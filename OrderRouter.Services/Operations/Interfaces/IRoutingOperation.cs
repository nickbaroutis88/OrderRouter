using OrderRouter.Services.Models;

namespace OrderRouter.Services.Operations.Interfaces;

public interface IRoutingOperation
{
    Task<RouteOrderResponse> RouteAsync(RouteOrderRequest request, CancellationToken cancellationToken = default);
}
