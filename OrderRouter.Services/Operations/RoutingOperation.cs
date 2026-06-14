using Microsoft.Extensions.Logging;
using OrderRouter.Services.Resolvers.Interfaces;
using OrderRouter.Services.Mappers.Interfaces;
using OrderRouter.Services.Models;
using OrderRouter.Services.Operations.Interfaces;
using OrderRouter.Services.Routing.Interfaces;

namespace OrderRouter.Services.Operations;

// Orchestrates the routing workflow:
//   1. Delegate Phase 1 (product resolution + supplier eligibility) to IOrderEligibilityResolver.
//   2. Delegate the assignment algorithm to IRoutingStrategy (swappable — see that interface).
//   3. Map the result to the response DTO via IRoutingMapper.
//
// This class has no direct database dependency — all DB access lives in IOrderEligibilityResolver.
// All routing algorithm logic lives in IRoutingStrategy.
public class RoutingOperation(
    IOrderEligibilityResolver eligibilityResolver,
    IRoutingStrategy strategy,
    IRoutingMapper mapper,
    ILogger<RoutingOperation> logger) : IRoutingOperation
{
    public async Task<RouteOrderResponse> RouteAsync(RouteOrderRequest request, CancellationToken cancellationToken = default)
    {
        // Input validation — checked before any DB work
        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
            return new RouteOrderResponse { OrderId = request.OrderId, Feasible = false, Errors = validationErrors };

        var customerZip = request.CustomerZip.Trim().PadLeft(5, '0');

        // Single pass over Items: builds the lookup and merges duplicate product codes by summing quantities
        var itemsByCode = new Dictionary<string, OrderItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in request.Items)
        {
            if (itemsByCode.TryGetValue(item.ProductCode, out var existing))
                itemsByCode[item.ProductCode] = new OrderItem { ProductCode = item.ProductCode, Quantity = existing.Quantity + item.Quantity };
            else
                itemsByCode[item.ProductCode] = item;
        }
        var productCodes = itemsByCode.Keys.ToList();

        // Phase 1 — resolve which suppliers are eligible for each product
        var eligibility = await eligibilityResolver.ResolveAsync(productCodes, customerZip, request.MailOrder, cancellationToken);

        if (eligibility.UnknownCodes.Count > 0 && !request.AllowPartial)
        {
            logger.LogWarning("Order {OrderId} references unknown product code(s): {Codes}", request.OrderId, eligibility.UnknownCodes);
            return Infeasible(request.OrderId, eligibility.UnknownCodes.Select(c => $"Unknown product: '{c}'"));
        }

        // Exclude unknown codes from the infeasible set — they carry a distinct "Unknown product" message
        // rather than "No supplier can fulfill". Without this separation, allow_partial: true would
        // emit "No supplier can fulfill 'FAKE-001'" for a code that simply doesn't exist in the catalogue.
        var unknownCodeSet = new HashSet<string>(eligibility.UnknownCodes, StringComparer.OrdinalIgnoreCase);
        var infeasibleCodes = eligibility.EligibilityMap
            .Where(kv => kv.Value.Count == 0 && !unknownCodeSet.Contains(kv.Key))
            .Select(kv => kv.Key)
            .ToList();

        if (infeasibleCodes.Count > 0 && !request.AllowPartial)
        {
            return Infeasible(request.OrderId, infeasibleCodes.Select(c => $"No supplier can fulfill '{c}' to ZIP {customerZip}"));
        }

        // Phase 2 — delegate to the registered routing strategy
        var assignments = strategy.Assign(eligibility.EligibilityMap);

        // Phase 3 — map results to response
        var routes = assignments
            .GroupBy(a => a.Value.Supplier.Id)
            .Select(g =>
            {
                var candidate = g.First().Value;
                var itemsForSupplier = g.Select(kv => itemsByCode[kv.Key]);
                return mapper.MapToSupplierRoute(candidate, itemsForSupplier);
            })
            .ToList();

        // When AllowPartial=true, both unknown and infeasible codes are reported in errors.
        // Unknown codes keep their "Unknown product" message; infeasible known codes use "No supplier can fulfill".
        var routingErrors = eligibility.UnknownCodes
            .Select(c => $"Unknown product: '{c}'")
            .Concat(infeasibleCodes.Select(c => $"No supplier can fulfill '{c}' to ZIP {customerZip}"))
            .ToList();

        return new RouteOrderResponse
        {
            OrderId = request.OrderId,
            // Feasible=false with a populated Routing list indicates a partial success:
            // AllowPartial=true was set and the strategy fulfilled only a subset of the order.
            // Errors will contain the product codes that could not be routed.
            Feasible = infeasibleCodes.Count == 0 && eligibility.UnknownCodes.Count == 0,
            Routing = routes.Count > 0 ? routes : null,
            Errors = routingErrors.Count > 0 ? routingErrors : null
        };
    }

    private static List<string> Validate(RouteOrderRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.CustomerZip))
            errors.Add("Order must include a valid customer_zip.");

        // TODO: validate customer_zip format against the postal standards of each supported market
        // (e.g. 5-digit numeric for US, alphanumeric for UK/CA). The current PadLeft normalization
        // above 'request.CustomerZip.Trim().PadLeft(5, '0')' is also US-specific and must be revisited alongside this validation.

        if (request.Items.Count == 0)
        {
            errors.Add("Order must include at least one line item.");
            return errors;
        }

        for (var i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];

            if (string.IsNullOrWhiteSpace(item.ProductCode))
                errors.Add($"Item at index {i} has an empty product_code.");

            if (item.Quantity < 1)
                errors.Add($"Item at index {i} has invalid quantity {item.Quantity} — must be >= 1.");
        }

        return errors;
    }

    private static RouteOrderResponse Infeasible(string orderId, IEnumerable<string> errors) =>
        new()
        {
            OrderId = orderId,
            Feasible = false,
            Errors = [..errors]
        };
}
