using Microsoft.EntityFrameworkCore;
using OrderRouter.Services.Store.Entities;

namespace OrderRouter.Services.Store.Contexts;

public class OrderRouterDbContext(DbContextOptions<OrderRouterDbContext> options) : DbContext(options)
{
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierCategory> SupplierCategories => Set<SupplierCategory>();
    public DbSet<SupplierZipCode> SupplierZipCodes => Set<SupplierZipCode>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SupplierId).IsUnique();
            entity.Property(e => e.SupplierId).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SupplierName).IsRequired();
        });

        modelBuilder.Entity<SupplierCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SupplierId, e.Category }).IsUnique();
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Supplier)
                  .WithMany(s => s.Categories)
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplierZipCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ZipCode);
            entity.HasIndex(e => new { e.SupplierId, e.ZipCode }).IsUnique();
            entity.Property(e => e.ZipCode).IsRequired().HasMaxLength(5);
            entity.HasOne(e => e.Supplier)
                  .WithMany(s => s.ZipCodes)
                  .HasForeignKey(e => e.SupplierId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProductCode).IsUnique();
            entity.Property(e => e.ProductCode).IsRequired().HasMaxLength(60);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
        });
    }
}
