using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OrderRouter.Services.Store.Contexts;

namespace OrderRouter.Api.HealthChecks;

// Verifies that the Suppliers and Products tables have been seeded with data.
//
// Returns Degraded (not Unhealthy) when a table is empty — the service is alive
// and will respond to requests, but every routing attempt will return infeasible
// until the missing data is loaded. Traffic continues to flow so callers and
// operators can observe the degraded state; the seeding pipeline should be investigated.
//
// Uses AnyAsync (EXISTS) rather than CountAsync (COUNT *) — we only need to know
// whether at least one row exists, so stopping at the first match is sufficient.
public class DataSeedingCheck(OrderRouterDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var hasSuppliers = await db.Suppliers.AnyAsync(cancellationToken);
        var hasProducts = await db.Products.AnyAsync(cancellationToken);

        if (!hasSuppliers && !hasProducts)
            return HealthCheckResult.Degraded("No suppliers or products have been loaded.");

        if (!hasSuppliers)
            return HealthCheckResult.Degraded("No suppliers have been loaded.");

        if (!hasProducts)
            return HealthCheckResult.Degraded("No products have been loaded.");

        return HealthCheckResult.Healthy("Suppliers and products are loaded.");
    }
}
