using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderRouter.Services.Resolvers;
using OrderRouter.Services.Resolvers.Interfaces;
using OrderRouter.Services.Mappers;
using OrderRouter.Services.Mappers.Interfaces;
using OrderRouter.Services.Operations;
using OrderRouter.Services.Operations.Interfaces;
using OrderRouter.Services.Routing;
using OrderRouter.Services.Routing.Interfaces;
using OrderRouter.Services.Store.Contexts;
using OrderRouter.Services.Store.Seeding;

namespace OrderRouter.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrderRouterServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Data store
        var connectionString = configuration.GetConnectionString("OrderRouterDb")
            ?? "Data Source=orderrouter.db";
        services.AddDbContext<OrderRouterDbContext>(opt => opt.UseSqlite(connectionString));

        // Seeding (scoped because they depend on DbContext)
        services.AddScoped<SupplierCsvParser>();
        services.AddScoped<ProductCsvParser>();
        services.AddScoped<DatabaseSeeder>();

        // Eligibility resolver — scoped to match DbContext lifetime
        services.AddScoped<IOrderEligibilityResolver, OrderEligibilityResolver>();

        // Routing strategy — registered as singleton because it is stateless.
        // To swap the algorithm, replace GreedySetCoverStrategy with any other IRoutingStrategy
        // implementation here without changing any other code.
        services.AddSingleton<IRoutingStrategy, GreedySetCoverStrategy>();

        // Mapper — stateless, singleton is appropriate
        services.AddSingleton<IRoutingMapper, RoutingMapper>();

        // Operations — scoped to match DbContext lifetime
        services.AddScoped<IRoutingOperation, RoutingOperation>();

        return services;
    }
}
