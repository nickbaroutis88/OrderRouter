namespace OrderRouter.Services.Store.Entities;

/// <summary>
/// Maps to the Suppliers table. One row per supplier.
/// Category and ZIP coverage are stored in separate join tables
/// (SupplierCategories, SupplierZipCodes) — see those entities for details.
/// </summary>
public class Supplier
{
    public int Id { get; set; }
    public required string SupplierId { get; set; }
    public required string SupplierName { get; set; }
    public bool CanMailOrder { get; set; }

    /// <summary>
    /// True when the supplier's service area covers all ZIP codes nationally.
    /// When true, SupplierZipCodes contains no rows for this supplier.
    /// </summary>
    public bool ServesAllZips { get; set; }
    public double? SatisfactionScore { get; set; }

    /// <summary>EF Core navigation — resolved via SupplierCategories join table. Does not add columns to this table.</summary>
    public ICollection<SupplierCategory> Categories { get; set; } = [];

    /// <summary>EF Core navigation — resolved via SupplierZipCodes join table. Empty when ServesAllZips is true.</summary>
    public ICollection<SupplierZipCode> ZipCodes { get; set; } = [];
}
