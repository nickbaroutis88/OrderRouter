using System.Runtime.Serialization;

namespace OrderRouter.Services.Models;

[DataContract]
public class RouteOrderResponse
{
    /// <summary>Identifier of the order that was processed, echoed from the request.</summary>
    [DataMember(Name = "order_id")]
    public required string OrderId { get; set; }

    /// <summary>
    /// True when every item in the request was successfully assigned to a supplier.
    /// False when one or more items could not be fulfilled (see Errors).
    /// When allow_partial=true, false may indicate a partial success — routing will contain
    /// the assignments that were possible, and errors will list the items that were skipped.
    /// </summary>
    [DataMember(Name = "feasible")]
    public bool Feasible { get; set; }

    /// <summary>Supplier routing assignments produced by the routing algorithm. Null when no items could be routed.</summary>
    [DataMember(Name = "routing", EmitDefaultValue = false)]
    public List<SupplierRoute>? Routing { get; set; }

    /// <summary>Human-readable messages describing why specific items could not be routed. Null when feasible is true.</summary>
    [DataMember(Name = "errors", EmitDefaultValue = false)]
    public List<string>? Errors { get; set; }
}
