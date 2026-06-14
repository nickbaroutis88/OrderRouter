using OrderRouter.Services.Routing;
using OrderRouter.Services.Store.Entities;
using Shouldly;

namespace OrderRouter.Services.Tests.UnitTests.Routing;

[TestClass]
public class GreedySetCoverStrategyTests
{
    private GreedySetCoverStrategy _sut = null!;

    [TestInitialize]
    public void Initialize() => _sut = new GreedySetCoverStrategy();

    // ── helpers ────────────────────────────────────────────────────────────────

    private static Supplier Sup(int id, string supplierId, double? score = null) =>
        new() { Id = id, SupplierId = supplierId, SupplierName = $"Supplier {supplierId}", SatisfactionScore = score };

    private static SupplierCandidate Cand(Supplier supplier, string mode = "local") =>
        new(supplier, mode);

    private static Dictionary<string, IReadOnlyList<SupplierCandidate>> Map(
        params (string Code, SupplierCandidate[] Candidates)[] entries) =>
        entries.ToDictionary(e => e.Code, e => (IReadOnlyList<SupplierCandidate>)e.Candidates);

    // ── edge cases ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Assign_EmptyEligibilityMap_ReturnsEmpty()
    {
        var result = _sut.Assign(Map());

        result.ShouldBeEmpty();
    }

    // ── single-supplier ────────────────────────────────────────────────────────

    [TestMethod]
    public void Assign_SingleSupplierOneProduct_AssignedCorrectly()
    {
        var sup = Sup(1, "SUP-001");
        var result = _sut.Assign(Map(("PROD-A", [Cand(sup)])));

        result.Count.ShouldBe(1);
        result["PROD-A"].Supplier.Id.ShouldBe(1);
    }

    [TestMethod]
    public void Assign_SingleSupplierMultipleProducts_AllAssigned()
    {
        var sup = Sup(1, "SUP-001");
        var result = _sut.Assign(Map(
            ("PROD-A", [Cand(sup)]),
            ("PROD-B", [Cand(sup)]),
            ("PROD-C", [Cand(sup)])));

        result.Count.ShouldBe(3);
        result.Values.ShouldAllBe(c => c.Supplier.Id == 1);
    }

    // ── multi-supplier ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Assign_TwoSuppliersDistinctProducts_BothRouted()
    {
        var supA = Sup(1, "SUP-001");
        var supB = Sup(2, "SUP-002");
        var result = _sut.Assign(Map(
            ("PROD-A", [Cand(supA)]),
            ("PROD-B", [Cand(supB)])));

        result["PROD-A"].Supplier.Id.ShouldBe(1);
        result["PROD-B"].Supplier.Id.ShouldBe(2);
    }

    // ── greedy coverage priority ───────────────────────────────────────────────

    [TestMethod]
    public void Assign_GreedyPrefersSupplierCoveringMostProducts()
    {
        var broad = Sup(1, "SUP-BROAD");  // covers 3 products
        var narrow = Sup(2, "SUP-NARROW"); // covers 1 product

        var result = _sut.Assign(Map(
            ("PROD-A", [Cand(broad), Cand(narrow)]),
            ("PROD-B", [Cand(broad)]),
            ("PROD-C", [Cand(broad)])));

        // broad supplier wins all 3 in one shipment
        result.Count.ShouldBe(3);
        result.Values.ShouldAllBe(c => c.Supplier.Id == 1);
    }

    // ── tie-breaking ───────────────────────────────────────────────────────────

    [TestMethod]
    public void Assign_TieBreaker_HigherSatisfactionScoreWins()
    {
        var highScore = Sup(1, "SUP-001", score: 9.0);
        var lowScore = Sup(2, "SUP-002", score: 7.0);

        var result = _sut.Assign(Map(
            ("PROD-A", [Cand(highScore), Cand(lowScore)])));

        result["PROD-A"].Supplier.Id.ShouldBe(1);
    }

    [TestMethod]
    public void Assign_TieBreaker_SameScore_LocalPreferredOverMailOrder()
    {
        var localSup = Sup(1, "SUP-LOCAL", score: 8.0);
        var mailSup = Sup(2, "SUP-MAIL", score: 8.0);

        var result = _sut.Assign(Map(
            ("PROD-A", [Cand(localSup, "local"), Cand(mailSup, "mail_order")])));

        result["PROD-A"].Supplier.Id.ShouldBe(1);
        result["PROD-A"].FulfillmentMode.ShouldBe("local");
    }

    // ── per-product mode selection ─────────────────────────────────────────────

    [TestMethod]
    public void Assign_WinnerOffersLocalAndMailOrder_LocalModeAssigned()
    {
        // same supplier appears with both modes for the same product
        var sup = Sup(1, "SUP-001");

        var result = _sut.Assign(Map(
            ("PROD-A", [Cand(sup, "mail_order"), Cand(sup, "local")])));

        result["PROD-A"].FulfillmentMode.ShouldBe("local");
    }
}
