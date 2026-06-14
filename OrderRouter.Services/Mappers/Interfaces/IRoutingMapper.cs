using OrderRouter.Services.Models;
using OrderRouter.Services.Routing;

namespace OrderRouter.Services.Mappers.Interfaces;

public interface IRoutingMapper
{
    // Maps a resolved supplier assignment and its assigned order items to the response DTO.
    SupplierRoute MapToSupplierRoute(
        SupplierCandidate assignment,
        IEnumerable<OrderItem> items);
}
