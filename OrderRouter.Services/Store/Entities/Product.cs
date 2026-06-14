namespace OrderRouter.Services.Store.Entities;

/// <summary>
/// Maps to the Products table. One row per product.
/// Category is stored as a normalised lowercase string and matched
/// against SupplierCategories during routing eligibility checks.
/// | Id | ProductCode | ProductName | Category |
/// </summary>
public class Product
{
    public int Id { get; set; }
    public required string ProductCode { get; set; }
    public required string ProductName { get; set; }
    public required string Category { get; set; }
}
