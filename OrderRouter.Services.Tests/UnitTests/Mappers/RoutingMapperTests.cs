using OrderRouter.Services.Mappers;
using OrderRouter.Services.Models;
using OrderRouter.Services.Routing;
using OrderRouter.Services.Store.Entities;
using Shouldly;

namespace OrderRouter.Services.Tests.UnitTests.Mappers;

[TestClass]
public class RoutingMapperTests
{
    private RoutingMapper _sut = null!;

    [TestInitialize]
    public void Initialize() => _sut = new RoutingMapper();

    // ── helpers ────────────────────────────────────────────────────────────────

    private static SupplierCandidate Candidate(
        string supplierId = "SUP-001",
        string supplierName = "Test Supplier",
        string mode = "local") =>
        new(new Supplier { SupplierId = supplierId, SupplierName = supplierName }, mode);

    private static OrderItem Item(string code = "WC-STD-001", int qty = 1) =>
        new() { ProductCode = code, Quantity = qty };

    // ── supplier identity ──────────────────────────────────────────────────────

    [TestMethod]
    public void MapToSupplierRoute_CopiesSupplierIdAndName()
    {
        var result = _sut.MapToSupplierRoute(
            Candidate(supplierId: "SUP-042", supplierName: "Acme Medical"),
            [Item()]);

        result.SupplierId.ShouldBe("SUP-042");
        result.SupplierName.ShouldBe("Acme Medical");
    }

    // ── fulfillment mode ───────────────────────────────────────────────────────

    [TestMethod]
    [DataRow("local")]
    [DataRow("mail_order")]
    public void MapToSupplierRoute_AppliesModeToEveryItem(string mode)
    {
        var items = new[] { Item("WC-STD-001"), Item("OX-PORT-024") };

        var result = _sut.MapToSupplierRoute(Candidate(mode: mode), items);

        result.Items.ShouldAllBe(i => i.FulfillmentMode == mode);
    }

    // ── item fields ────────────────────────────────────────────────────────────

    [TestMethod]
    public void MapToSupplierRoute_PreservesProductCodeAndQuantityPerItem()
    {
        var items = new[] { Item("WC-STD-001", 2), Item("OX-PORT-024", 5) };

        var result = _sut.MapToSupplierRoute(Candidate(), items);

        result.Items[0].ProductCode.ShouldBe("WC-STD-001");
        result.Items[0].Quantity.ShouldBe(2);
        result.Items[1].ProductCode.ShouldBe("OX-PORT-024");
        result.Items[1].Quantity.ShouldBe(5);
    }

    // ── item count ─────────────────────────────────────────────────────────────

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(10)]
    public void MapToSupplierRoute_ItemCountMatchesInput(int count)
    {
        var items = Enumerable.Range(1, count).Select(i => Item($"PROD-{i:D3}"));

        var result = _sut.MapToSupplierRoute(Candidate(), items);

        result.Items.Count.ShouldBe(count);
    }

    // ── edge case ──────────────────────────────────────────────────────────────

    [TestMethod]
    public void MapToSupplierRoute_EmptyItems_ReturnsRouteWithNoItems()
    {
        var result = _sut.MapToSupplierRoute(Candidate(), []);

        result.Items.ShouldBeEmpty();
    }
}
