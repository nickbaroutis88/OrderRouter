using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderRouter.Services.Store.Contexts;

public class OrderRouterDbContextFactory : IDesignTimeDbContextFactory<OrderRouterDbContext>
{
    public OrderRouterDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrderRouterDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new OrderRouterDbContext(options);
    }
}
