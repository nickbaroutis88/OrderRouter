using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using OrderRouter.Services.Store.Entities;
using System.Globalization;

namespace OrderRouter.Services.Store.Seeding;

public class SupplierCsvParser(ILogger<SupplierCsvParser> logger)
{
    // Any single ZIP range wider than this is treated as national coverage
    private const int NationalZipThreshold = 5000;

    public IReadOnlyList<ParsedSupplier> Parse(string filePath)
    {
        StreamReader reader;

        try
        {
            reader = new StreamReader(filePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read suppliers CSV at {Path}", filePath);
            return [];
        }

        using (reader)
        {
            return ParseInternal(reader);
        }
    }

    private IReadOnlyList<ParsedSupplier> ParseInternal(TextReader textReader)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            // Normalise known header variations from source data
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant() switch
            {
                "suplier_name"    => "supplier_name",   // typo in source data
                "can_mail_order?" => "can_mail_order",  // trailing ? in source data
                var h             => h
            },
            MissingFieldFound = null,
        };

        using var csv = new CsvReader(textReader, config);

        try
        {
            csv.Read();
            csv.ReadHeader();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read suppliers CSV header.");
            return [];
        }

        var required = new[] { "supplier_id", "supplier_name", "service_zips", "product_categories", "customer_satisfaction_score", "can_mail_order" };
        var headers = csv.HeaderRecord!
            .Select(h => config.PrepareHeaderForMatch(new PrepareHeaderForMatchArgs(h, 0)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(c => !headers.Contains(c)).ToList();

        if (missing.Count > 0)
        {
            logger.LogError("Suppliers CSV is missing required columns: {Columns}", string.Join(", ", missing));
            return [];
        }

        var results = new List<ParsedSupplier>();

        while (csv.Read())
        {
            var lineNumber = csv.Context.Parser?.Row ?? 0;
            var parsed = ParseRow(csv, lineNumber);
            if (parsed is not null)
                results.Add(parsed);
        }

        return results;
    }

    private ParsedSupplier? ParseRow(CsvReader csv, int lineNumber)
    {
        try
        {
            var supplierId        = (csv.GetField("supplier_id") ?? "").Trim();
            var supplierName      = (csv.GetField("supplier_name") ?? "").Trim();
            var canMailOrderRaw   = (csv.GetField("can_mail_order") ?? "").Trim();
            var scoreRaw          = (csv.GetField("customer_satisfaction_score") ?? "").Trim();
            var serviceZipsRaw    = (csv.GetField("service_zips") ?? "").Trim();
            var categoriesRaw     = (csv.GetField("product_categories") ?? "").Trim();

            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(supplierId))
                issues.Add("supplier_id is empty");

            if (string.IsNullOrWhiteSpace(supplierName))
                issues.Add("supplier_name is empty");

            if (!canMailOrderRaw.Equals("y", StringComparison.OrdinalIgnoreCase) &&
                !canMailOrderRaw.Equals("n", StringComparison.OrdinalIgnoreCase))
                issues.Add($"can_mail_order has unrecognised value '{canMailOrderRaw}' (expected y/n)");

            if (string.IsNullOrWhiteSpace(serviceZipsRaw))
                issues.Add("service_zips is empty");

            if (string.IsNullOrWhiteSpace(categoriesRaw))
                issues.Add("product_categories is empty");

            if (issues.Count > 0)
            {
                logger.LogWarning(
                    "Skipping row {Line} — {IssueCount} issue(s): {Issues}.",
                    lineNumber, issues.Count, string.Join("; ", issues));
                return null;
            }

            double? score = double.TryParse(scoreRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var scoreValue)
                ? scoreValue
                : null;

            if (!string.IsNullOrWhiteSpace(scoreRaw) && score is null)
                logger.LogWarning("Row {Line}: could not parse customer_satisfaction_score '{Score}' — defaulting to null.", lineNumber, scoreRaw);
            var canMailOrder = canMailOrderRaw.Equals("y", StringComparison.OrdinalIgnoreCase);
            var (zipCodes, servesAllZips) = ParseZips(serviceZipsRaw);
            var categories = ParseCategories(categoriesRaw);

            return new ParsedSupplier(supplierId, supplierName, canMailOrder, servesAllZips, score, zipCodes, categories);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipping malformed supplier row at line {Line}.", lineNumber);
            return null;
        }
    }

    // TODO: ZIP normalisation is US-only — PadLeft(5,'0') handles leading zeros (e.g. "501" → "00501")
    // but is meaningless for alphanumeric postal codes (UK, Canada, etc.).
    // Revisit alongside RoutingOperation when adding support for non-US markets.
    private (List<string> ZipCodes, bool ServesAllZips) ParseZips(string zipField)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var zipCodes = new List<string>();

        if (string.IsNullOrWhiteSpace(zipField))
            return (zipCodes, false);

        var parts = zipField.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            if (trimmed.Contains('-'))
            {
                var sides = trimmed.Split('-', 2);
                if (sides.Length == 2
                    && int.TryParse(sides[0].Trim(), out int start)
                    && int.TryParse(sides[1].Trim(), out int end))
                {
                    if (end - start >= NationalZipThreshold)
                        return ([], true);

                    for (int z = start; z <= end; z++)
                    {
                        var zip = z.ToString().PadLeft(5, '0');
                        if (seen.Add(zip)) zipCodes.Add(zip);
                    }
                }
                else
                {
                    logger.LogWarning("Could not parse ZIP range segment: '{Segment}'", trimmed);
                }
            }
            else
            {
                if (int.TryParse(trimmed, out int single))
                {
                    var zip = single.ToString().PadLeft(5, '0');
                    if (seen.Add(zip)) zipCodes.Add(zip);
                }
                else
                    logger.LogWarning("Could not parse ZIP code: '{Zip}'", trimmed);
            }
        }

        return (zipCodes, false);
    }

    private static List<string> ParseCategories(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
           .Select(c => c.Trim().ToLowerInvariant())
           .Where(c => !string.IsNullOrEmpty(c))
           .Distinct()
           .ToList();
}

public record ParsedSupplier(
    string SupplierId,
    string SupplierName,
    bool CanMailOrder,
    bool ServesAllZips,
    double? SatisfactionScore,
    List<string> ZipCodes,
    List<string> Categories);
