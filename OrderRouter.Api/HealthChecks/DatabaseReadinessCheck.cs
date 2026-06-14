using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderRouter.Services.Store.Contexts;

namespace OrderRouter.Api.HealthChecks;

// Verifies that the SQLite database file is accessible.
// Registered under the "ready" tag — if unhealthy, /health/ready returns 503
// and the k8s readiness probe stops routing traffic to this pod until the
// database becomes reachable.
public class DatabaseReadinessCheck(OrderRouterDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await db.Database.CanConnectAsync(cancellationToken);
            return HealthCheckResult.Healthy("Database is accessible.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database is not accessible.", ex);
        }
    }
}
