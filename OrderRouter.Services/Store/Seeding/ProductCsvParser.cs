using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace OrderRouter.Services.Store.Seeding;

public class ProductCsvParser(ILogger<ProductCsvParser> logger)
{
    public IReadOnlyList<ParsedProduct> Parse(string filePath)
    {
        StreamReader reader;

        try
        {
            reader = new StreamReader(filePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read products CSV at {Path}", filePath);
            return [];
        }

        using (reader)
        {
            return ParseInternal(reader);
        }
    }

    private IReadOnlyList<ParsedProduct> ParseInternal(TextReader textReader)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
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
            logger.LogError(ex, "Failed to read products CSV header.");
            return [];
        }

        var required = new[] { "product_code", "product_name", "category" };
        var headers = csv.HeaderRecord!
            .Select(h => h.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(c => !headers.Contains(c)).ToList();

        if (missing.Count > 0)
        {
            logger.LogError("Products CSV is missing required columns: {Columns}", string.Join(", ", missing));
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<ParsedProduct>();

        while (csv.Read())
        {
            var lineNumber = csv.Context.Parser?.Row ?? 0;
            var parsed = ParseRow(csv, lineNumber);
            if (parsed is null) continue;

            if (!seen.Add(parsed.ProductCode))
            {
                logger.LogWarning("Duplicate product_code '{Code}' at line {Line} — skipping.", parsed.ProductCode, lineNumber);
                continue;
            }

            results.Add(parsed);
        }

        return results;
    }

    private ParsedProduct? ParseRow(CsvReader csv, int lineNumber)
    {
        try
        {
            var productCode = (csv.GetField("product_code") ?? "").Trim();
            var productName = (csv.GetField("product_name") ?? "").Trim();
            var category = (csv.GetField("category") ?? "").Trim().ToLowerInvariant();

            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(productCode))
                issues.Add("product_code is empty");

            if (string.IsNullOrWhiteSpace(productName))
                issues.Add("product_name is empty");

            if (string.IsNullOrWhiteSpace(category))
                issues.Add("category is empty");

            if (issues.Count > 0)
            {
                logger.LogWarning(
                    "Skipping row {Line} — {IssueCount} issue(s): {Issues}.",
                    lineNumber, issues.Count, string.Join("; ", issues));
                return null;
            }

            return new ParsedProduct(productCode, productName, category);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Skipping malformed product row at line {Line}.", lineNumber);
            return null;
        }
    }
}

public record ParsedProduct(
    string ProductCode,
    string ProductName,
    string Category);
