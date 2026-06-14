namespace OrderRouter.Services.Store.Entities;

/// <summary>
/// Maps to the SupplierZipCodes join table.
/// Each row represents one ZIP code within a supplier's local service area.
/// Suppliers with ServesAllZips=true have no rows here — their national coverage
/// is captured on the Supplier record itself to avoid expanding ~99,900 rows per supplier.
/// | Id | SupplierId (FK) | ZipCode |
/// </summary>
public class SupplierZipCode
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public required string ZipCode { get; set; }

    /// <summary>EF Core navigation back to the parent Supplier. No extra column or table — enables cascade delete.</summary>
    public Supplier Supplier { get; set; } = null!;
}
