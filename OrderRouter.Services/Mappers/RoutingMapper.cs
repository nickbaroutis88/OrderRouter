using OrderRouter.Services.Mappers.Interfaces;
using OrderRouter.Services.Models;
using OrderRouter.Services.Routing;

namespace OrderRouter.Services.Mappers;

public class RoutingMapper : IRoutingMapper
{
    public SupplierRoute MapToSupplierRoute(
        SupplierCandidate assignment,
        IEnumerable<OrderItem> items)
    {
        return new SupplierRoute
        {
            SupplierId = assignment.Supplier.SupplierId,
            SupplierName = assignment.Supplier.SupplierName,
            Items = items.Select(item => new RoutedItem
            {
                ProductCode = item.ProductCode,
                Quantity = item.Quantity,
                FulfillmentMode = assignment.FulfillmentMode
            }).ToList()
        };
    }
}
