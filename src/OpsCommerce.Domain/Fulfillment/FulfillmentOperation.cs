using OpsCommerce.Domain.Common;

namespace OpsCommerce.Domain.Fulfillment;

public enum FulfillmentType
{
    OrderShipment = 1,
    StockTransfer = 2,
    ProductionTransfer = 3,
    ServiceTransfer = 4,
    InternalMovement = 5
}

public enum FulfillmentStatus
{
    Draft = 1,
    Planned = 2,
    Approved = 3,
    Assigned = 4,
    PickedUp = 5,
    InTransit = 6,
    Arrived = 7,
    Completed = 8,
    Cancelled = 9,
    Failed = 10
}

/// <summary>
/// A delivery / movement operation (order shipment, internal move, …).
/// When an order is fully paid, a fulfillment operation is created
/// automatically in Draft.
///
/// Assigning a courier moves it to Assigned, and from there the courier
/// advances it step by step: PickedUp → InTransit → Arrived → Completed.
/// The transition map enforces the order of the steps — a courier cannot
/// mark a delivery Completed without arriving first — and a courier can
/// only ever advance deliveries assigned to them (checked in the service).
/// </summary>
public class FulfillmentOperation : AuditableEntity
{
    public Guid CompanyId { get; private set; }
    public FulfillmentType Type { get; private set; }
    public FulfillmentStatus Status { get; private set; }

    public Guid? SourceLocationId { get; private set; }
    public Guid? DestinationLocationId { get; private set; }
    public Guid? RelatedOrderId { get; private set; }
    public Guid RequestedByUserId { get; private set; }

    public string? AssignedCarrierType { get; private set; }
    public Guid? AssignedCourierId { get; private set; }
    public string? Notes { get; private set; }

    private FulfillmentOperation() { }

    public FulfillmentOperation(
        Guid companyId,
        FulfillmentType type,
        Guid? sourceLocationId,
        Guid? destinationLocationId,
        Guid? relatedOrderId,
        Guid requestedByUserId,
        string? assignedCarrierType,
        string? notes)
    {
        CompanyId = companyId;
        Type = type;
        Status = FulfillmentStatus.Draft;
        SourceLocationId = sourceLocationId;
        DestinationLocationId = destinationLocationId;
        RelatedOrderId = relatedOrderId;
        RequestedByUserId = requestedByUserId;
        AssignedCarrierType = assignedCarrierType;
        Notes = notes;
    }

    private static readonly IReadOnlyDictionary<FulfillmentStatus, FulfillmentStatus[]> Transitions =
        new Dictionary<FulfillmentStatus, FulfillmentStatus[]>
        {
            [FulfillmentStatus.Draft]     = [FulfillmentStatus.Planned, FulfillmentStatus.Assigned, FulfillmentStatus.Cancelled],
            [FulfillmentStatus.Planned]   = [FulfillmentStatus.Approved, FulfillmentStatus.Assigned, FulfillmentStatus.Cancelled],
            [FulfillmentStatus.Approved]  = [FulfillmentStatus.Assigned, FulfillmentStatus.Cancelled],
            [FulfillmentStatus.Assigned]  = [FulfillmentStatus.PickedUp, FulfillmentStatus.Cancelled, FulfillmentStatus.Failed],
            [FulfillmentStatus.PickedUp]  = [FulfillmentStatus.InTransit, FulfillmentStatus.Failed],
            [FulfillmentStatus.InTransit] = [FulfillmentStatus.Arrived, FulfillmentStatus.Failed],
            [FulfillmentStatus.Arrived]   = [FulfillmentStatus.Completed, FulfillmentStatus.Failed],
            [FulfillmentStatus.Completed] = [],
            [FulfillmentStatus.Cancelled] = [],
            [FulfillmentStatus.Failed]    = [],
        };

    /// <summary>Guarded transition — an invalid status jump throws (HTTP 422 at the API).</summary>
    public void ChangeStatus(FulfillmentStatus status)
    {
        StateMachine.EnsureTransition(Transitions, Status, status, "Fulfillment");
        if (Status == status) return;
        Status = status;
        SetUpdated();
    }

    public void AssignCarrier(string carrierType)
    {
        AssignedCarrierType = carrierType;
        SetUpdated();
    }

    /// <summary>Assigns a courier and moves the operation to Assigned.</summary>
    public void AssignCourier(Guid courierId)
    {
        AssignedCourierId = courierId;
        ChangeStatus(FulfillmentStatus.Assigned);
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        SetUpdated();
    }
}
