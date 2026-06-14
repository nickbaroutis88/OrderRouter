using Microsoft.Extensions.Logging.Abstractions;
using OrderRouter.Services.Store.Seeding;
using Shouldly;

namespace OrderRouter.Services.Tests.UnitTests.Store.Seeding;

[TestClass]
public class ProductCsvParserTests
{
    // ── helper ─────────────────────────────────────────────────────────────────

    private static IReadOnlyList<ParsedProduct> ParseCsv(string content)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, content);
            return new ProductCsvParser(NullLogger<ProductCsvParser>.Instance).Parse(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private const string Headers = "product_code,product_name,category";

    // ── happy path ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_ValidRow_MapsAllFields()
    {
        var csv = $"{Headers}\nWC-STD-001,Standard Wheelchair,wheelchair";

        var result = ParseCsv(csv);

        result.Count.ShouldBe(1);
        var p = result[0];
        p.ProductCode.ShouldBe("WC-STD-001");
        p.ProductName.ShouldBe("Standard Wheelchair");
        p.Category.ShouldBe("wheelchair");
    }

    [TestMethod]
    public void Parse_MultipleValidRows_AllReturned()
    {
        var csv = $"{Headers}\nWC-STD-001,Standard Wheelchair,wheelchair\nOX-PORT-024,Portable Oxygen,oxygen";

        var result = ParseCsv(csv);

        result.Count.ShouldBe(2);
    }

    // ── category normalization ─────────────────────────────────────────────────

    [TestMethod]
    public void Parse_Category_NormalizedToLowercase()
    {
        var csv = $"{Headers}\nWC-STD-001,Standard Wheelchair,Wheelchair";

        var result = ParseCsv(csv);

        result[0].Category.ShouldBe("wheelchair");
    }

    [TestMethod]
    public void Parse_Category_TrimmedBeforeLowercasing()
    {
        var csv = $"{Headers}\nWC-STD-001,Standard Wheelchair,  WHEELCHAIR  ";

        var result = ParseCsv(csv);

        result[0].Category.ShouldBe("wheelchair");
    }

    // ── duplicate detection ────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_DuplicateProductCode_SecondRowSkipped()
    {
        var csv = $"{Headers}\nWC-STD-001,Standard Wheelchair,wheelchair\nWC-STD-001,Duplicate Entry,wheelchair";

        var result = ParseCsv(csv);

        result.Count.ShouldBe(1);
        result[0].ProductName.ShouldBe("Standard Wheelchair");
    }

    [TestMethod]
    public void Parse_DuplicateProductCodeCaseInsensitive_SecondRowSkipped()
    {
        var csv = $"{Headers}\nwc-std-001,Standard Wheelchair,wheelchair\nWC-STD-001,Duplicate Entry,wheelchair";

        var result = ParseCsv(csv);

        result.Count.ShouldBe(1);
    }

    // ── row skipping ───────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_EmptyProductCode_RowSkipped()
    {
        var csv = $"{Headers}\n,Standard Wheelchair,wheelchair";

        var result = ParseCsv(csv);

        result.ShouldBeEmpty();
    }

    [TestMethod]
    public void Parse_EmptyProductName_RowSkipped()
    {
        var csv = $"{Headers}\nWC-STD-001,,wheelchair";

        var result = ParseCsv(csv);

        result.ShouldBeEmpty();
    }

    [TestMethod]
    public void Parse_EmptyCategory_RowSkipped()
    {
        var csv = $"{Headers}\nWC-STD-001,Standard Wheelchair,";

        var result = ParseCsv(csv);

        result.ShouldBeEmpty();
    }

    [TestMethod]
    public void Parse_InvalidRowDoesNotPreventOtherRows()
    {
        var csv = $"{Headers}\n,Bad Row,wheelchair\nWC-STD-001,Standard Wheelchair,wheelchair";

        var result = ParseCsv(csv);

        result.Count.ShouldBe(1);
        result[0].ProductCode.ShouldBe("WC-STD-001");
    }

    // ── missing required column ────────────────────────────────────────────────

    [TestMethod]
    public void Parse_MissingRequiredColumn_ReturnsEmptyList()
    {
        // category deliberately omitted — must not use Headers constant
        var csv = "product_code,product_name\nWC-STD-001,Standard Wheelchair";

        var result = ParseCsv(csv);

        result.ShouldBeEmpty();
    }
}
