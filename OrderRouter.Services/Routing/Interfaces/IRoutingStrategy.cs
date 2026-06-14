namespace OrderRouter.Services.Routing.Interfaces;

// Encapsulates the supplier-assignment algorithm behind a swappable interface.
//
// Architectural decision: the routing algorithm is intentionally decoupled from
// RoutingOperation so that alternative strategies (e.g. exact set-cover via ILP,
// cost-optimized assignment, A/B-tested variants) can be registered in DI without
// modifying any orchestration or mapping code.
//
// The default implementation is GreedySetCoverStrategy.
public interface IRoutingStrategy
{
    // Given a pre-computed eligibility map (product_code → eligible suppliers),
    // returns one SupplierCandidate per product code.
    // Products that cannot be assigned are absent from the result — the caller
    // must handle them as infeasible.
    IReadOnlyDictionary<string, SupplierCandidate> Assign(
        IReadOnlyDictionary<string, IReadOnlyList<SupplierCandidate>> eligibility);
}
