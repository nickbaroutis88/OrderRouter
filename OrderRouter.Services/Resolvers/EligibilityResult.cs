using OrderRouter.Services.Routing;

namespace OrderRouter.Services.Resolvers;

/// <summary>
/// Output of IOrderEligibilityResolver. Carries the per-product candidate map
/// and any product codes that could not be found in the catalogue.
/// </summary>
public record EligibilityResult(
    IReadOnlyDictionary<string, IReadOnlyList<SupplierCandidate>> EligibilityMap,
    IReadOnlyList<string> UnknownCodes
);
