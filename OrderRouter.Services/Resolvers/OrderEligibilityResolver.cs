using Microsoft.EntityFrameworkCore;
using OrderRouter.Services.Resolvers.Interfaces;
using OrderRouter.Services.Routing;
using OrderRouter.Services.Store.Contexts;
using OrderRouter.Services.Store.Entities;

namespace OrderRouter.Services.Resolvers;

public class OrderEligibilityResolver(OrderRouterDbContext db) : IOrderEligibilityResolver
{
    public async Task<EligibilityResult> ResolveAsync(
        IReadOnlyList<string> productCodes,
        string customerZip,
        bool mailOrderAllowed,
        CancellationToken cancellationToken = default)
    {
        // Phase 1a — resolve products (single DB query)
        var products = await db.Products
            .Where(p => productCodes.Contains(p.ProductCode))
            .ToDictionaryAsync(p => p.ProductCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var unknownCodes = productCodes
            .Except(products.Keys, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Phase 1b — load eligible suppliers for known products (single DB query)
        var categories = products.Values.Select(p => p.Category).Distinct().ToList();

        var eligibleSuppliers = await db.Suppliers
            .Include(s => s.Categories)
            .Include(s => s.ZipCodes)
            .AsSplitQuery()
            .Where(s =>
                s.Categories.Any(c => categories.Contains(c.Category)) &&
                (s.ServesAllZips || s.ZipCodes.Any(z => z.ZipCode == customerZip) || (s.CanMailOrder && mailOrderAllowed)))
            .ToListAsync(cancellationToken);

        // Phase 1c — build eligibility map in memory
        var eligibilityMap = BuildEligibilityMap(productCodes, products, eligibleSuppliers, customerZip, mailOrderAllowed);

        return new EligibilityResult(eligibilityMap, unknownCodes);
    }

    // Answers: "For each product in this order, which suppliers can fulfill it and via what mode?"
    //
    // Suppliers are indexed by category first (outer loop) so that each product resolves its
    // candidates with a single O(1) dictionary lookup (inner loop).
    // Overall complexity: O(S*C + P) — S suppliers, C categories per supplier, P products.
    // Reversing the order (products outer, suppliers inner) would scan all suppliers for every
    // product and produce O(P*S), which compounds as both catalogues grow.
    internal static IReadOnlyDictionary<string, IReadOnlyList<SupplierCandidate>> BuildEligibilityMap(
        IReadOnlyList<string> productCodes,
        Dictionary<string, Product> products,
        List<Supplier> eligibleSuppliers,
        string customerZip,
        bool mailOrderAllowed)
    {
        // Pass 1 — index suppliers by category, O(S*C)
        var candidatesByCategory = new Dictionary<string, List<SupplierCandidate>>(StringComparer.OrdinalIgnoreCase);

        foreach (var supplier in eligibleSuppliers)
        {
            // Fulfillment mode is resolved once per supplier — it depends only on customer ZIP, not on product.
            bool servesLocal = supplier.ServesAllZips || supplier.ZipCodes.Any(z => z.ZipCode == customerZip);

            if (!servesLocal && !(supplier.CanMailOrder && mailOrderAllowed))
                continue;

            var candidate = new SupplierCandidate(supplier, servesLocal ? "local" : "mail_order");

            foreach (var sc in supplier.Categories)
            {
                if (!candidatesByCategory.TryGetValue(sc.Category, out var list))
                    candidatesByCategory[sc.Category] = list = [];

                list.Add(candidate);
            }
        }

        // Pass 2 — resolve each product against the index, O(P)
        var map = new Dictionary<string, IReadOnlyList<SupplierCandidate>>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in productCodes)
        {
            if (!products.TryGetValue(code, out var product))
            {
                map[code] = [];
                continue;
            }

            map[code] = candidatesByCategory.TryGetValue(product.Category, out var candidates)
                ? candidates
                : [];
        }

        return map;
    }
}
