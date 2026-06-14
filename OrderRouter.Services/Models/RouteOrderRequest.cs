using System.Runtime.Serialization;

namespace OrderRouter.Services.Models;

[DataContract]
public class RouteOrderRequest
{
    /// <summary>Unique identifier for the order, used to correlate the response.</summary>
    [DataMember(Name = "order_id")]
    public required string OrderId { get; set; }

    /// <summary>Customer postal code. Format requirements depend on the supported market (e.g. 5-digit ZIP for US).</summary>
    [DataMember(Name = "customer_zip")]
    public required string CustomerZip { get; set; }

    /// <summary>One or more items to be routed to suppliers.</summary>
    [DataMember(Name = "items")]
    public required List<OrderItem> Items { get; set; }

    /// <summary>
    /// Whether mail-order suppliers are eligible for this order.
    /// True: local and mail-order suppliers are both considered (local preferred).
    /// False: only suppliers that physically serve the customer ZIP are considered.
    /// Defaults to true.
    /// </summary>
    [DataMember(Name = "mail_order")]
    public bool MailOrder { get; set; } = true;

    /// <summary>
    /// Controls behaviour when one or more items cannot be fulfilled.
    /// False (default): any infeasible item aborts the whole order and returns feasible=false with no routing.
    /// True: feasible items are routed; infeasible items are listed in Errors with feasible=false.
    /// </summary>
    [DataMember(Name = "allow_partial")]
    public bool AllowPartial { get; set; } = false;
}
