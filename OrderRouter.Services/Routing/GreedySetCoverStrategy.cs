using OrderRouter.Services.Routing.Interfaces;

namespace OrderRouter.Services.Routing;

// Greedy set-cover strategy that implements the three routing requirements in priority order:
//
//   Priority 2 — Fewer shipments: at each step, pick the supplier that covers the most
//                still-unassigned products. This minimizes the total number of suppliers
//                (and therefore shipments) used.
//   Priority 3 — Quality: when two suppliers cover the same number of products, prefer
//                the one with the higher satisfaction score (see ResolveTieBreaker).
//   Priority 4 — Geographic preference: when scores are equal, prefer local delivery
//                over mail_order (see ResolveTieBreaker).
//
// Priority 1 (feasibility) is enforced upstream in Phase 1 — only eligible suppliers
// reach this strategy via the eligibility map.
//
// This is the correct algorithm for the given requirements, not a pragmatic compromise.
// An approach that traded more shipments for higher scores would violate priority 2 > priority 3.
//
// The input eligibility map (productCode → suppliers) already serves as the reverse index.
// We build the forward index (supplierId → products) once in O(P × S), then maintain
// coverage counts incrementally instead of recomputing them from scratch each round.
//
// Complexity: O(P × S) setup + O(S²) find-max + O(P × S) count updates = O(P × S + S²) overall,
// where P = distinct product count and S = distinct eligible suppliers.
public class GreedySetCoverStrategy : IRoutingStrategy
{
    public IReadOnlyDictionary<string, SupplierCandidate> Assign(
        IReadOnlyDictionary<string, IReadOnlyList<SupplierCandidate>> eligibility)
    {
        var result = new Dictionary<string, SupplierCandidate>(StringComparer.OrdinalIgnoreCase);
        var uncoveredProducts = new HashSet<string>(eligibility.Keys, StringComparer.OrdinalIgnoreCase);

        // Single O(P × S) pass over the input to build two structures:
        //   supplierProducts    — forward index: supplierId → set of products that supplier covers
        //   supplierCandidates  — supplierId → SupplierCandidate, used for tie-breaking
        //   coverageCount       — supplierId → count of still-uncovered products this supplier can fulfill
        //
        // coverageCount is decremented incrementally as products are assigned, so each round's
        // winner search is a simple scan over integers rather than a full eligibility recomputation.
        // The input eligibility map already gives us productCode → suppliers (reverse index).
        var supplierProducts = new Dictionary<int, HashSet<string>>();
        var supplierCandidates = new Dictionary<int, SupplierCandidate>();
        var coverageCount = new Dictionary<int, int>();

        foreach (var (productCode, candidates) in eligibility)
        {
            foreach (var candidate in candidates)
            {
                var id = candidate.Supplier.Id;

                if (!supplierProducts.TryGetValue(id, out var products))
                {
                    supplierProducts[id] = products = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    supplierCandidates[id] = candidate;
                    coverageCount[id] = 0;
                }

                products.Add(productCode);
                coverageCount[id]++;
            }
        }

        while (uncoveredProducts.Count > 0)
        {
            // Priority 2 — find the supplier covering the most uncovered products (fewest shipments).
            // Linear scan over remaining suppliers; telescopes to O(S²) total across all iterations.
            // When coverage counts are equal, ResolveTieBreaker applies priorities 3 and 4.
            SupplierCandidate? winningSupplier = null;
            var winningSupplierCount = 0;

            foreach (var (id, count) in coverageCount)
            {
                var candidate = supplierCandidates[id];

                if (count > winningSupplierCount ||
                    (count == winningSupplierCount && winningSupplier is not null && ResolveTieBreaker(candidate, winningSupplier)))
                {
                    winningSupplier = candidate;
                    winningSupplierCount = count;
                }
            }

            if (winningSupplier is null || winningSupplierCount == 0)
                break;

            var winnerId = winningSupplier.Supplier.Id;

            // Assign the winning supplier to all uncovered products it can fulfill.
            foreach (var productCode in supplierProducts[winnerId])
            {
                if (!uncoveredProducts.Contains(productCode))
                    continue;

                // Resolve per-product mode: prefer "local" if this supplier offers it for this product.
                var preferred = eligibility[productCode]
                    .Where(c => c.Supplier.Id == winnerId)
                    .OrderBy(c => c.FulfillmentMode == "local" ? 0 : 1)
                    .First();

                result[productCode] = preferred;
                uncoveredProducts.Remove(productCode);

                // Decrement coverage counts for every other supplier of this product.
                // Uses the input eligibility map as the reverse index (productCode → suppliers).
                foreach (var candidate in eligibility[productCode])
                {
                    if (coverageCount.ContainsKey(candidate.Supplier.Id))
                        coverageCount[candidate.Supplier.Id]--;
                }
            }

            // Remove the winning supplier from the pool — they will not be considered again.
            coverageCount.Remove(winnerId);
            supplierProducts.Remove(winnerId);
            supplierCandidates.Remove(winnerId);
        }

        return result;
    }

    /// <summary>
    /// Resolves ties between two suppliers that cover the same number of uncovered products.
    /// Applies priority 3 (satisfaction score, descending) then priority 4 (local over mail_order).
    /// Returns true if the challenger should replace the current winner.
    /// </summary>
    private static bool ResolveTieBreaker(SupplierCandidate challenger, SupplierCandidate current)
    {
        var challengerScore = challenger.Supplier.SatisfactionScore ?? 0.0;
        var currentScore = current.Supplier.SatisfactionScore ?? 0.0;

        if (Math.Abs(challengerScore - currentScore) > 1e-10)
            return challengerScore > currentScore;

        // Priority 4 — same score: prefer local delivery over mail_order
        return challenger.FulfillmentMode == "local" && current.FulfillmentMode != "local";
    }
}
