using System.Runtime.Serialization;

namespace OrderRouter.Services.Models;

[DataContract]
public class RoutedItem
{
    /// <summary>Product code of the item being fulfilled.</summary>
    [DataMember(Name = "product_code")]
    public required string ProductCode { get; set; }

    /// <summary>Number of units to be fulfilled by this supplier.</summary>
    [DataMember(Name = "quantity")]
    public int Quantity { get; set; }

    /// <summary>
    /// How this item will be delivered.
    /// "local"      — supplier physically serves the customer ZIP.
    /// "mail_order" — supplier ships nationally; customer ZIP is outside their local service area.
    /// </summary>
    /// <remarks>
    /// For a given order, all items under the same supplier will always share the same value
    /// because fulfillment mode is determined by supplier coverage of the customer_zip, not by product.
    /// This could be moved up to SupplierRoute level — confirm with product before simplifying.
    /// </remarks>
    [DataMember(Name = "fulfillment_mode")]
    public required string FulfillmentMode { get; set; }
}
