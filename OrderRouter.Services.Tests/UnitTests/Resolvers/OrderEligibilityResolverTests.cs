using OrderRouter.Services.Resolvers;
using OrderRouter.Services.Routing;
using OrderRouter.Services.Store.Entities;
using Shouldly;

namespace OrderRouter.Services.Tests.UnitTests.Resolvers;

[TestClass]
public class OrderEligibilityResolverTests
{
    // Tests target BuildEligibilityMap — the pure in-memory logic that maps
    // products to eligible suppliers. The DB query phase is thin EF Core
    // infrastructure and is not duplicated here.

    // ── helpers ────────────────────────────────────────────────────────────────

    private static IReadOnlyDictionary<string, IReadOnlyList<SupplierCandidate>> Build(
        string[] productCodes,
        Dictionary<string, Product> products,
        List<Supplier> suppliers,
        string zip = "10001",
        bool mailOrderAllowed = true) =>
        OrderEligibilityResolver.BuildEligibilityMap(productCodes, products, suppliers, zip, mailOrderAllowed);

    private static Product MakeProduct(string code, string category) =>
        new() { ProductCode = code, ProductName = code, Category = category };

    private static Supplier LocalSupplier(int id, string supplierId, string[] categories, string[] zips) =>
        new()
        {
            Id = id, SupplierId = supplierId, SupplierName = supplierId,
            Categories = categories.Select(c => new SupplierCategory { Category = c }).ToList(),
            ZipCodes = zips.Select(z => new SupplierZipCode { ZipCode = z }).ToList()
        };

    private static Supplier MailOrderSupplier(int id, string supplierId, string[] categories) =>
        new()
        {
            Id = id, SupplierId = supplierId, SupplierName = supplierId,
            CanMailOrder = true,
            Categories = categories.Select(c => new SupplierCategory { Category = c }).ToList(),
            ZipCodes = []
        };

    private static Supplier NationalSupplier(int id, string supplierId, string[] categories) =>
        new()
        {
            Id = id, SupplierId = supplierId, SupplierName = supplierId,
            ServesAllZips = true,
            Categories = categories.Select(c => new SupplierCategory { Category = c }).ToList(),
            ZipCodes = []
        };

    // ── unknown product codes ──────────────────────────────────────────────────

    [TestMethod]
    public void BuildEligibilityMap_UnknownProductCode_MappedToEmptyList()
    {
        // no products in catalogue — code is unknown
        var result = Build(["FAKE-001"], products: [], suppliers: []);

        result["FAKE-001"].ShouldBeEmpty();
    }

    // ── local supplier ─────────────────────────────────────────────────────────

    [TestMethod]
    public void BuildEligibilityMap_LocalSupplierServesZip_ReturnedWithLocalMode()
    {
        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase)
        {
            ["PROD-A"] = MakeProduct("PROD-A", "wheelchair")
        };
        var suppliers = new List<Supplier>
        {
            LocalSupplier(1, "SUP-001", ["wheelchair"], ["10001"])
        };

        var result = Build(["PROD-A"], products, suppliers, zip: "10001");

        var candidates = result["PROD-A"];
        candidates.Count.ShouldBe(1);
        candidates[0].Supplier.SupplierId.ShouldBe("SUP-001");
        candidates[0].FulfillmentMode.ShouldBe("local");
    }

    [TestMethod]
    public void BuildEligibilityMap_LocalSupplierWrongZip_NotEligible()
    {
        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase)
        {
            ["PROD-A"] = MakeProduct("PROD-A", "wheelchair")
        };
        var suppliers = new List<Supplier>
        {
            LocalSupplier(1, "SUP-001", ["wheelchair"], ["10001"])
        };

        var result = Build(["PROD-A"], products, suppliers, zip: "99999", mailOrderAllowed: false);

        result["PROD-A"].ShouldBeEmpty();
    }

    // ── mail-order supplier ────────────────────────────────────────────────────

    [TestMethod]
    public void BuildEligibilityMap_MailOrderSupplier_MailOrderAllowed_ReturnedWithMailOrderMode()
    {
        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase)
        {
            ["PROD-A"] = MakeProduct("PROD-A", "wheelchair")
        };
        var suppliers = new List<Supplier>
        {
            MailOrderSupplier(1, "SUP-MAIL", ["wheelchair"])
        };

        var result = Build(["PROD-A"], products, suppliers, zip: "99999", mailOrderAllowed: true);

        var candidates = result["PROD-A"];
        candidates.Count.ShouldBe(1);
        candidates[0].FulfillmentMode.ShouldBe("mail_order");
    }

    [TestMethod]
    public void BuildEligibilityMap_MailOrderSupplier_MailOrderNotAllowed_NotEligible()
    {
        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase)
        {
            ["PROD-A"] = MakeProduct("PROD-A", "wheelchair")
        };
        var suppliers = new List<Supplier>
        {
            MailOrderSupplier(1, "SUP-MAIL", ["wheelchair"])
        };

        var result = Build(["PROD-A"], products, suppliers, zip: "99999", mailOrderAllowed: false);

        result["PROD-A"].ShouldBeEmpty();
    }

    // ── national supplier ──────────────────────────────────────────────────────

    [TestMethod]
    public void BuildEligibilityMap_NationalSupplier_EligibleForAnyZipWithLocalMode()
    {
        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase)
        {
            ["PROD-A"] = MakeProduct("PROD-A", "wheelchair")
        };
        var suppliers = new List<Supplier>
        {
            NationalSupplier(1, "SUP-NAT", ["wheelchair"])
        };

        var result = Build(["PROD-A"], products, suppliers, zip: "99999", mailOrderAllowed: false);

        var candidates = result["PROD-A"];
        candidates.Count.ShouldBe(1);
        candidates[0].FulfillmentMode.ShouldBe("local");
    }

    // ── category matching ──────────────────────────────────────────────────────

    [TestMethod]
    public void BuildEligibilityMap_SupplierWrongCategory_NotEligible()
    {
        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase)
        {
            ["PROD-A"] = MakeProduct("PROD-A", "wheelchair")
        };
        var suppliers = new List<Supplier>
        {
            LocalSupplier(1, "SUP-001", ["oxygen"], ["10001"])  // different category
        };

        var result = Build(["PROD-A"], products, suppliers, zip: "10001");

        result["PROD-A"].ShouldBeEmpty();
    }

    [TestMethod]
    public void BuildEligibilityMap_MultipleEligibleSuppliers_AllReturned()
    {
        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase)
        {
            ["PROD-A"] = MakeProduct("PROD-A", "wheelchair")
        };
        var suppliers = new List<Supplier>
        {
            LocalSupplier(1, "SUP-001", ["wheelchair"], ["10001"]),
            LocalSupplier(2, "SUP-002", ["wheelchair"], ["10001"])
        };

        var result = Build(["PROD-A"], products, suppliers, zip: "10001");

        result["PROD-A"].Count.ShouldBe(2);
    }

    // ── mixed products ─────────────────────────────────────────────────────────

    [TestMethod]
    public void BuildEligibilityMap_MixedKnownAndUnknown_EachMappedCorrectly()
    {
        var products = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase)
        {
            ["PROD-A"] = MakeProduct("PROD-A", "wheelchair")
        };
        var suppliers = new List<Supplier>
        {
            LocalSupplier(1, "SUP-001", ["wheelchair"], ["10001"])
        };

        var result = Build(["PROD-A", "FAKE-001"], products, suppliers, zip: "10001");

        result["PROD-A"].Count.ShouldBe(1);
        result["FAKE-001"].ShouldBeEmpty();
    }
}
