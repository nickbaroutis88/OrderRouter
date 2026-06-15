using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderRouter.Services.Store.Contexts;
using OrderRouter.Services.Store.Entities;

namespace OrderRouter.Services.Store.Seeding;

public class DatabaseSeeder(
    OrderRouterDbContext context,
    SupplierCsvParser supplierParser,
    ProductCsvParser productParser,
    IConfiguration configuration,
    ILogger<DatabaseSeeder> logger)
{
    private const string DefaultSuppliersPath = "/app/data/suppliers.csv";
    private const string DefaultProductsPath = "/app/data/products.csv";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await context.Database.MigrateAsync(cancellationToken);

        bool suppliersEmpty = !await context.Suppliers.AnyAsync(cancellationToken);
        bool productsEmpty = !await context.Products.AnyAsync(cancellationToken);

        if (!suppliersEmpty && !productsEmpty)
        {
            logger.LogDebug("Database is already seeded — skipping.");
            return;
        }

        if (suppliersEmpty)
        {
            logger.LogWarning("Suppliers table is empty. Seeding from CSV.");
            await SeedSuppliersAsync(cancellationToken);
        }

        if (productsEmpty)
        {
            logger.LogWarning("Products table is empty. Seeding from CSV.");
            await SeedProductsAsync(cancellationToken);
        }
    }

    private async Task SeedSuppliersAsync(CancellationToken cancellationToken)
    {
        var path = configuration["DataFiles:SuppliersPath"] ?? DefaultSuppliersPath;

        if (!File.Exists(path))
        {
            logger.LogWarning("Suppliers CSV not found at '{Path}'. Service will start without supplier data.", path);
            return;
        }

        var parsed = supplierParser.Parse(path);

        if (parsed.Count == 0)
        {
            logger.LogWarning("Suppliers CSV at '{Path}' produced no valid records. Service will start without supplier data.", path);
            return;
        }

        var suppliers = parsed.Select(p => new Supplier
        {
            SupplierId = p.SupplierId,
            SupplierName = p.SupplierName,
            CanMailOrder = p.CanMailOrder,
            ServesAllZips = p.ServesAllZips,
            SatisfactionScore = p.SatisfactionScore,
            Categories = p.Categories.Select(c => new SupplierCategory { Category = c }).ToList(),
            ZipCodes = p.ServesAllZips
                ? []
                : p.ZipCodes.Select(z => new SupplierZipCode { ZipCode = z }).ToList()
        }).ToList();

        await context.Suppliers.AddRangeAsync(suppliers, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            logger.LogDebug("Seeded {Count} suppliers.", suppliers.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save suppliers to the database. Service will start without supplier data.");
        }
    }

    private async Task SeedProductsAsync(CancellationToken cancellationToken)
    {
        var path = configuration["DataFiles:ProductsPath"] ?? DefaultProductsPath;

        if (!File.Exists(path))
        {
            logger.LogWarning("Products CSV not found at '{Path}'. Service will start without product data.", path);
            return;
        }

        var parsed = productParser.Parse(path);

        if (parsed.Count == 0)
        {
            logger.LogWarning("Products CSV at '{Path}' produced no valid records. Service will start without product data.", path);
            return;
        }

        var products = parsed.Select(p => new Product
        {
            ProductCode = p.ProductCode,
            ProductName = p.ProductName,
            Category = p.Category
        }).ToList();

        await context.Products.AddRangeAsync(products, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} products.", products.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save products to the database. Service will start without product data.");
        }
    }
}
