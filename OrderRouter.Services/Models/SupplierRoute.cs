using System.Runtime.Serialization;

namespace OrderRouter.Services.Models;

[DataContract]
public class SupplierRoute
{
    /// <summary>Business identifier of the assigned supplier (from the suppliers data file).</summary>
    [DataMember(Name = "supplier_id")]
    public required string SupplierId { get; set; }

    /// <summary>Display name of the assigned supplier.</summary>
    [DataMember(Name = "supplier_name")]
    public required string SupplierName { get; set; }

    /// <summary>Items assigned to this supplier for fulfillment.</summary>
    [DataMember(Name = "items")]
    public List<RoutedItem> Items { get; set; } = [];
}
