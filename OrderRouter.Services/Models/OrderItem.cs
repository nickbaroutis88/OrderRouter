using System.Runtime.Serialization;

namespace OrderRouter.Services.Models;

[DataContract]
public class OrderItem
{
    /// <summary>Product code that identifies the item to be fulfilled.</summary>
    [DataMember(Name = "product_code")]
    public required string ProductCode { get; set; }

    /// <summary>Number of units requested. Defaults to 1.</summary>
    [DataMember(Name = "quantity")]
    public int Quantity { get; set; } = 1;
}
