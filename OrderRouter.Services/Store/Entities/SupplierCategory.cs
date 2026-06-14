namespace OrderRouter.Services.Store.Entities;

/// <summary>
/// Maps to the SupplierCategories join table.
/// Each row represents one category that a supplier carries.
/// A supplier with 3 categories produces 3 rows here.
/// | Id | SupplierId (FK) | Category |
/// </summary>
public class SupplierCategory
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public required string Category { get; set; }

    /// <summary>EF Core navigation back to the parent Supplier. No extra column or table — enables cascade delete.</summary>
    public Supplier Supplier { get; set; } = null!;
}
