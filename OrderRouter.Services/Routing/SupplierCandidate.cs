using OrderRouter.Services.Store.Entities;

namespace OrderRouter.Services.Routing;

// Represents a supplier that is eligible to fulfill a specific product,
// together with the mode of delivery that applies for the requested ZIP.
public record SupplierCandidate(Supplier Supplier, string FulfillmentMode);
