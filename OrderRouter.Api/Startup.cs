using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderRouter.Api.HealthChecks;
using OrderRouter.Services.Extensions;

namespace OrderRouter.Api;

// Startup deliberately separates DI registration and pipeline configuration
// from the entry point (Program.cs).
//
// Architectural decision: although .NET 6+ made Startup optional by inlining
// everything in Program.cs, keeping Startup as an explicit class means:
//   - Program.cs stays a 5-line bootstrap that is never touched for feature work.
//   - All cross-cutting concerns (auth, CORS, health checks, Swagger) have one
//     obvious home that can be reviewed and extended without scrolling past
//     application code.
//   - Startup can be instantiated and its methods called in integration tests
//     to get a fully-configured test host without duplicating wiring logic.
public class Startup(IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(opts =>
            {
                // Use snake_case for all JSON property names — matches the DataMember(Name = ...) labels on the models.
                // System.Text.Json does not read DataMember attributes natively; the naming policy achieves the same result.
                opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        services.AddSwaggerGen(c =>
            c.SwaggerDoc("v1", new() { Title = "OrderRouter API", Version = "v1" }));

        services.AddOrderRouterServices(configuration);

        services.AddHealthChecks()
            .AddCheck<DatabaseReadinessCheck>("db_ready", tags: ["ready"])
            .AddCheck<DataSeedingCheck>("data_seeded", tags: ["ready"]);
    }

    public void Configure(WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapControllers();

        // Readiness probe — used by k8s to decide whether to route traffic to this pod.
        //
        // Healthy  (200): database accessible and both tables populated.
        // Degraded (200): database accessible but a table is empty — traffic flows so callers
        //                 can observe the state; every routing attempt will return infeasible
        //                 until the seeding pipeline is fixed.
        // Unhealthy (503): database not accessible — k8s stops routing traffic until recovered.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        });
    }
}
