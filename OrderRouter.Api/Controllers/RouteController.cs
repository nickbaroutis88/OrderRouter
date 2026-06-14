using Microsoft.AspNetCore.Mvc;
using OrderRouter.Services.Models;
using OrderRouter.Services.Operations.Interfaces;

namespace OrderRouter.Api.Controllers;

[ApiController]
[Route("api/route")]
public class RouteController(IRoutingOperation routingOperation) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] RouteOrderRequest request,
        CancellationToken cancellationToken)
    {
        var response = await routingOperation.RouteAsync(request, cancellationToken);

        return Ok(response);
    }
}
