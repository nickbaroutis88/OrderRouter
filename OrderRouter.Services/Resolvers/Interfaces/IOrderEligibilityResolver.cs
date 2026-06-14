using OrderRouter.Services.Resolvers;

namespace OrderRouter.Services.Resolvers.Interfaces;

// Resolves which suppliers are eligible to fulfill each product in an order.
// Encapsulates all Phase 1 database access and in-memory eligibility logic so
// that RoutingOperation stays a pure orchestrator with no direct DB dependency,
// and so this concern can be tested independently.
public interface IOrderEligibilityResolver
{
    Task<EligibilityResult> ResolveAsync(
        IReadOnlyList<string> productCodes,
        string customerZip,
        bool mailOrderAllowed,
        CancellationToken cancellationToken = default);
}
