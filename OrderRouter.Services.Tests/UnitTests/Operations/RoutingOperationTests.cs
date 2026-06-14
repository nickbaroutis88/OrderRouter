using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderRouter.Services.Mappers.Interfaces;
using OrderRouter.Services.Models;
using OrderRouter.Services.Operations;
using OrderRouter.Services.Resolvers;
using OrderRouter.Services.Resolvers.Interfaces;
using OrderRouter.Services.Routing;
using OrderRouter.Services.Routing.Interfaces;
using OrderRouter.Services.Store.Entities;
using Shouldly;

namespace OrderRouter.Services.Tests.UnitTests.Operations;

[TestClass]
public class RoutingOperationTests
{
    private Mock<IOrderEligibilityResolver> _resolver = null!;
    private Mock<IRoutingStrategy> _strategy = null!;
    private Mock<IRoutingMapper> _mapper = null!;
    private RoutingOperation _sut = null!;

    [TestInitialize]
    public void Initialize()
    {
        _resolver = new Mock<IOrderEligibilityResolver>();
        _strategy = new Mock<IRoutingStrategy>();
        _mapper = new Mock<IRoutingMapper>();
        _sut = new RoutingOperation(
            _resolver.Object, _strategy.Object, _mapper.Object,
            NullLogger<RoutingOperation>.Instance);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static RouteOrderRequest SimpleRequest(
        string zip = "10001",
        string productCode = "PROD-A",
        bool allowPartial = false) =>
        new()
        {
            OrderId = "ORD-001",
            CustomerZip = zip,
            Items = [new OrderItem { ProductCode = productCode, Quantity = 1 }],
            AllowPartial = allowPartial
        };

    private EligibilityResult EmptyEligibility() =>
        new(new Dictionary<string, IReadOnlyList<SupplierCandidate>>(), []);

    private EligibilityResult WithUnknownCode(string code) =>
        new(new Dictionary<string, IReadOnlyList<SupplierCandidate>>(), [code]);

    private EligibilityResult WithInfeasibleCode(string code) =>
        new(new Dictionary<string, IReadOnlyList<SupplierCandidate>>
        {
            [code] = []    // known product, but no eligible supplier
        }, []);

    private EligibilityResult Routable(string code, SupplierCandidate candidate) =>
        new(new Dictionary<string, IReadOnlyList<SupplierCandidate>>
        {
            [code] = [candidate]
        }, []);

    private static SupplierCandidate MakeCandidate(int id = 1) =>
        new(new Supplier { Id = id, SupplierId = "SUP-001", SupplierName = "Acme" }, "local");

    // ── input validation ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task RouteAsync_EmptyCustomerZip_ReturnsFeasibleFalseWithError()
    {
        var request = SimpleRequest(zip: "");

        var result = await _sut.RouteAsync(request);

        result.Feasible.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Contains("customer_zip"));
        _resolver.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RouteAsync_EmptyItemsList_ReturnsFeasibleFalseWithError()
    {
        var request = new RouteOrderRequest { OrderId = "ORD-001", CustomerZip = "10001", Items = [] };

        var result = await _sut.RouteAsync(request);

        result.Feasible.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Contains("at least one line item"));
        _resolver.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RouteAsync_ItemWithEmptyProductCode_ReturnsFeasibleFalseWithError()
    {
        var request = SimpleRequest(productCode: "");

        var result = await _sut.RouteAsync(request);

        result.Feasible.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Contains("empty product_code"));
        _resolver.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task RouteAsync_ItemWithZeroQuantity_ReturnsFeasibleFalseWithError()
    {
        var request = new RouteOrderRequest
        {
            OrderId = "ORD-001", CustomerZip = "10001",
            Items = [new OrderItem { ProductCode = "PROD-A", Quantity = 0 }]
        };

        var result = await _sut.RouteAsync(request);

        result.Feasible.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Contains("invalid quantity"));
        _resolver.VerifyNoOtherCalls();
    }

    // ── ZIP normalization ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task RouteAsync_ShortCustomerZip_PaddedBeforePassedToResolver()
    {
        string? capturedZip = null;
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, bool, CancellationToken>((_, zip, _, _) => capturedZip = zip)
            .ReturnsAsync(EmptyEligibility());
        _strategy.Setup(s => s.Assign(It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<SupplierCandidate>>>()))
            .Returns(new Dictionary<string, SupplierCandidate>());

        await _sut.RouteAsync(SimpleRequest(zip: "501"));

        capturedZip.ShouldBe("00501");
    }

    // ── duplicate product code merging ─────────────────────────────────────────

    [TestMethod]
    public async Task RouteAsync_DuplicateProductCodes_QuantitiesMerged()
    {
        IReadOnlyList<string>? capturedCodes = null;
        _resolver
            .Setup(r => r.ResolveAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, bool, CancellationToken>((codes, _, _, _) => capturedCodes = codes)
            .ReturnsAsync(EmptyEligibility());
        _strategy.Setup(s => s.Assign(It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<SupplierCandidate>>>()))
            .Returns(new Dictionary<string, SupplierCandidate>());

        var request = new RouteOrderRequest
        {
            OrderId = "ORD-001", CustomerZip = "10001",
            Items =
            [
                new OrderItem { ProductCode = "PROD-A", Quantity = 2 },
                new OrderItem { ProductCode = "PROD-A", Quantity = 3 }
            ]
        };

        await _sut.RouteAsync(request);

        // resolver receives one distinct code, not two
        capturedCodes!.Count.ShouldBe(1);
        capturedCodes[0].ShouldBe("PROD-A");
    }

    // ── unknown product codes ──────────────────────────────────────────────────

    [TestMethod]
    public async Task RouteAsync_UnknownCode_AllowPartialFalse_InfeasibleWithUnknownError()
    {
        _resolver.Setup(r => r.ResolveAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WithUnknownCode("FAKE-001"));

        var result = await _sut.RouteAsync(SimpleRequest(productCode: "FAKE-001"));

        result.Feasible.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Contains("Unknown product") && e.Contains("FAKE-001"));
        result.Routing.ShouldBeNull();
    }

    [TestMethod]
    public async Task RouteAsync_UnknownCode_AllowPartialTrue_RoutingNullAndErrorReported()
    {
        _resolver.Setup(r => r.ResolveAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WithUnknownCode("FAKE-001"));
        _strategy.Setup(s => s.Assign(It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<SupplierCandidate>>>()))
            .Returns(new Dictionary<string, SupplierCandidate>());

        var result = await _sut.RouteAsync(SimpleRequest(productCode: "FAKE-001", allowPartial: true));

        result.Feasible.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Contains("Unknown product") && e.Contains("FAKE-001"));
    }

    // ── infeasible product codes ───────────────────────────────────────────────

    [TestMethod]
    public async Task RouteAsync_InfeasibleProduct_AllowPartialFalse_InfeasibleWithNoSupplierError()
    {
        _resolver.Setup(r => r.ResolveAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WithInfeasibleCode("PROD-A"));

        var result = await _sut.RouteAsync(SimpleRequest(productCode: "PROD-A", zip: "99999"));

        result.Feasible.ShouldBeFalse();
        result.Errors.ShouldNotBeNull();
        result.Errors.ShouldContain(e => e.Contains("No supplier can fulfill") && e.Contains("PROD-A"));
        result.Routing.ShouldBeNull();
    }

    // ── successful routing ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task RouteAsync_AllProductsRouted_FeasibleTrue()
    {
        var candidate = MakeCandidate();
        _resolver.Setup(r => r.ResolveAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Routable("PROD-A", candidate));
        _strategy.Setup(s => s.Assign(It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<SupplierCandidate>>>()))
            .Returns(new Dictionary<string, SupplierCandidate> { ["PROD-A"] = candidate });
        _mapper.Setup(m => m.MapToSupplierRoute(It.IsAny<SupplierCandidate>(), It.IsAny<IEnumerable<OrderItem>>()))
            .Returns(new SupplierRoute { SupplierId = "SUP-001", SupplierName = "Acme" });

        var result = await _sut.RouteAsync(SimpleRequest());

        result.Feasible.ShouldBeTrue();
        result.Routing.ShouldNotBeNull();
        result.Routing!.Count.ShouldBe(1);
        result.Errors.ShouldBeNull();
    }

    [TestMethod]
    public async Task RouteAsync_TwoSuppliersDistinctProducts_TwoRoutesReturned()
    {
        var candA = MakeCandidate(id: 1);
        var candB = new SupplierCandidate(
            new Supplier { Id = 2, SupplierId = "SUP-002", SupplierName = "Beta" }, "local");

        _resolver.Setup(r => r.ResolveAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EligibilityResult(
                new Dictionary<string, IReadOnlyList<SupplierCandidate>>
                {
                    ["PROD-A"] = [candA],
                    ["PROD-B"] = [candB]
                }, []));
        _strategy.Setup(s => s.Assign(It.IsAny<IReadOnlyDictionary<string, IReadOnlyList<SupplierCandidate>>>()))
            .Returns(new Dictionary<string, SupplierCandidate> { ["PROD-A"] = candA, ["PROD-B"] = candB });
        _mapper.Setup(m => m.MapToSupplierRoute(candA, It.IsAny<IEnumerable<OrderItem>>()))
            .Returns(new SupplierRoute { SupplierId = "SUP-001", SupplierName = "Acme" });
        _mapper.Setup(m => m.MapToSupplierRoute(candB, It.IsAny<IEnumerable<OrderItem>>()))
            .Returns(new SupplierRoute { SupplierId = "SUP-002", SupplierName = "Beta" });

        var request = new RouteOrderRequest
        {
            OrderId = "ORD-001", CustomerZip = "10001",
            Items =
            [
                new OrderItem { ProductCode = "PROD-A", Quantity = 1 },
                new OrderItem { ProductCode = "PROD-B", Quantity = 1 }
            ]
        };

        var result = await _sut.RouteAsync(request);

        result.Feasible.ShouldBeTrue();
        result.Routing!.Count.ShouldBe(2);
        result.Errors.ShouldBeNull();
    }
}
