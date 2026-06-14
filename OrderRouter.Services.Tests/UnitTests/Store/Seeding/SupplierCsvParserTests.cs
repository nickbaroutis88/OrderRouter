using Microsoft.Extensions.Logging.Abstractions;
using OrderRouter.Services.Store.Seeding;
using Shouldly;

namespace OrderRouter.Services.Tests.UnitTests.Store.Seeding;

[TestClass]
public class SupplierCsvParserTests
{
    // ── helper ─────────────────────────────────────────────────────────────────

    private static IReadOnlyList<ParsedSupplier> ParseCsv(string content)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, content);
            return new SupplierCsvParser(NullLogger<SupplierCsvParser>.Instance).Parse(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private const string Headers =
        "supplier_id,supplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order";

    // ── happy path ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_ValidRow_MapsAllFields()
    {
        var csv = $"{Headers}\nSUP-001,Acme Medical,10001,wheelchair,8.5,y";

        var result = ParseCsv(csv);

        result.Count.ShouldBe(1);
        var s = result[0];
        s.SupplierId.ShouldBe("SUP-001");
        s.SupplierName.ShouldBe("Acme Medical");
        s.CanMailOrder.ShouldBeTrue();
        s.SatisfactionScore.ShouldBe(8.5);
        s.ServesAllZips.ShouldBeFalse();
        s.ZipCodes.ShouldContain("10001");
        s.Categories.ShouldContain("wheelchair");
    }

    [TestMethod]
    public void Parse_MultipleValidRows_AllReturned()
    {
        var csv = $"{Headers}\nSUP-001,Supplier A,10001,wheelchair,8.0,y\nSUP-002,Supplier B,10002,oxygen,7.5,n";

        var result = ParseCsv(csv);

        result.Count.ShouldBe(2);
    }

    // ── header normalization ───────────────────────────────────────────────────

    [TestMethod]
    public void Parse_SupplierNameTypo_NormalizedCorrectly()
    {
        // intentional typo in header — must not use Headers constant
        var csv = "suplier_name,supplier_id,service_zips,product_categories,customer_satisfaction_score,can_mail_order" +
                  "\nAcme Medical,SUP-001,10001,wheelchair,8.5,y";

        var result = ParseCsv(csv);

        result.Count.ShouldBe(1);
        result[0].SupplierName.ShouldBe("Acme Medical");
    }

    [TestMethod]
    public void Parse_CanMailOrderWithTrailingQuestion_NormalizedCorrectly()
    {
        // trailing '?' in header — must not use Headers constant
        var csv = "supplier_id,supplier_name,service_zips,product_categories,customer_satisfaction_score,can_mail_order?" +
                  "\nSUP-001,Acme Medical,10001,wheelchair,8.5,y";

        var result = ParseCsv(csv);

        result.Count.ShouldBe(1);
        result[0].CanMailOrder.ShouldBeTrue();
    }

    // ── can_mail_order ─────────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("y", true)]
    [DataRow("Y", true)]
    [DataRow("n", false)]
    [DataRow("N", false)]
    public void Parse_CanMailOrder_CaseInsensitive(string value, bool expected)
    {
        var csv = $"{Headers}\nSUP-001,Acme Medical,10001,wheelchair,8.5,{value}";

        var result = ParseCsv(csv);

        result[0].CanMailOrder.ShouldBe(expected);
    }

    // ── satisfaction score ─────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_SatisfactionScore_Parsed()
    {
        var csv = $"{Headers}\nSUP-001,Acme Medical,10001,wheelchair,9.2,y";

        var result = ParseCsv(csv);

        result[0].SatisfactionScore.ShouldBe(9.2);
    }

    [TestMethod]
    [DataRow("no ratings yet")]
    [DataRow("")]
    public void Parse_UnparsableSatisfactionScore_ReturnsNull(string score)
    {
        var csv = $"{Headers}\nSUP-001,Acme Medical,10001,wheelchair,{score},y";

        var result = ParseCsv(csv);

        result[0].SatisfactionScore.ShouldBeNull();
    }

    // ── ZIP parsing ────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_ZipRange_ExpandedToIndividualZips()
    {
        var csv = $"{Headers}\nSUP-001,Acme Medical,10001-10003,wheelchair,8.5,y";

        var result = ParseCsv(csv);

        result[0].ZipCodes.ShouldBe(["10001", "10002", "10003"], ignoreOrder: true);
    }

    [TestMethod]
    public void Parse_NationalZipRange_SetsServesAllZips()
    {
        var csv = $"{Headers}\nSUP-001,Acme Medical,00100-99999,wheelchair,8.5,y";

        var result = ParseCsv(csv);

        result[0].ServesAllZips.ShouldBeTrue();
        result[0].ZipCodes.ShouldBeEmpty();
    }

    [TestMethod]
    public void Parse_OverlappingZipRanges_DeduplicatesZips()
    {
        var csv = $"{Headers}\nSUP-001,Acme Medical,\"10001-10003,10002-10004\",wheelchair,8.5,y";

        var result = ParseCsv(csv);

        result[0].ZipCodes.ShouldBe(["10001", "10002", "10003", "10004"], ignoreOrder: true);
    }

    [TestMethod]
    public void Parse_SingleZip_LeadingZeroPreserved()
    {
        var csv = $"{Headers}\nSUP-001,Acme Medical,501,wheelchair,8.5,y";

        var result = ParseCsv(csv);

        result[0].ZipCodes.ShouldContain("00501");
    }

    // ── categories ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_Categories_LowercasedAndTrimmed()
    {
        var csv = $"{Headers}\nSUP-001,Acme Medical,10001,\"  Wheelchair , CPAP  \",8.5,y";

        var result = ParseCsv(csv);

        result[0].Categories.ShouldBe(["wheelchair", "cpap"], ignoreOrder: true);
    }

    // ── row skipping ───────────────────────────────────────────────────────────

    [TestMethod]
    public void Parse_EmptySupplierId_RowSkipped()
    {
        var csv = $"{Headers}\n,Acme Medical,10001,wheelchair,8.5,y";

        var result = ParseCsv(csv);

        result.ShouldBeEmpty();
    }

    [TestMethod]
    public void Parse_InvalidCanMailOrderValue_RowSkipped()
    {
        var csv = $"{Headers}\nSUP-001,Acme Medical,10001,wheelchair,8.5,maybe";

        var result = ParseCsv(csv);

        result.ShouldBeEmpty();
    }

    [TestMethod]
    public void Parse_MissingRequiredColumn_ReturnsEmptyList()
    {
        // customer_satisfaction_score deliberately omitted — must not use Headers constant
        var csv = "supplier_id,supplier_name,service_zips,product_categories,can_mail_order\n" +
                  "SUP-001,Acme Medical,10001,wheelchair,y";

        var result = ParseCsv(csv);

        result.ShouldBeEmpty();
    }
}
