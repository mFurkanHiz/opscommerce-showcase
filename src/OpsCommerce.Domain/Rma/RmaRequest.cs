using OpsCommerce.Domain.Common;

namespace OpsCommerce.Domain.Rma;

public enum RmaType
{
    /// <summary>Return: refund + product goes back into stock.</summary>
    Return = 1,
    /// <summary>Exchange: product goes back into stock, a replacement is sent.</summary>
    Exchange = 2,
    /// <summary>Repair: no stock or refund effect.</summary>
    Repair = 3
}

public enum RmaStatus
{
    Requested = 1,
    Approved = 2,
    Rejected = 3,
    Completed = 4,
    Cancelled = 5
}

/// <summary>
/// A return / exchange / repair request (RMA) raised against an order —
/// by a registered customer or by a guest who proves ownership with the
/// order's guest token.
///
/// Lifecycle: Requested → Approved → Completed (or Rejected / Cancelled).
/// Completing an RMA applies the real effects in one transaction:
/// returned goods are received back into inventory, and for returns the
/// order moves into the refund flow. Business limits are enforced up
/// front — you cannot return more units than were ordered, and a refund
/// can never exceed what was actually paid.
/// </summary>
public class RmaRequest : AuditableEntity
{
    public Guid CompanyId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public string? GuestToken { get; private set; }
    public RmaType Type { get; private set; }
    public int Quantity { get; private set; }
    public string Reason { get; private set; } = null!;
    public RmaStatus Status { get; private set; }
    public Guid? RestockLocationId { get; private set; }
    public decimal? RefundAmount { get; private set; }
    public string? ResolutionNote { get; private set; }

    private static readonly IReadOnlyDictionary<RmaStatus, RmaStatus[]> Transitions =
        new Dictionary<RmaStatus, RmaStatus[]>
        {
            [RmaStatus.Requested] = [RmaStatus.Approved, RmaStatus.Rejected, RmaStatus.Cancelled],
            [RmaStatus.Approved]  = [RmaStatus.Completed, RmaStatus.Cancelled],
            [RmaStatus.Rejected]  = [],
            [RmaStatus.Completed] = [],
            [RmaStatus.Cancelled] = [],
        };

    private RmaRequest() { }

    public RmaRequest(
        Guid companyId, Guid orderId, Guid? productId, Guid? customerId, string? guestToken,
        RmaType type, int quantity, string reason, Guid? restockLocationId)
    {
        if (quantity <= 0)
            throw new BusinessRuleException("RMA quantity must be positive.", "RMA_QTY_INVALID");
        if (string.IsNullOrWhiteSpace(reason))
            throw new BusinessRuleException("RMA reason is required.", "RMA_REASON_REQUIRED");

        CompanyId = companyId;
        OrderId = orderId;
        ProductId = productId;
        CustomerId = customerId;
        GuestToken = guestToken;
        Type = type;
        Quantity = quantity;
        Reason = reason;
        RestockLocationId = restockLocationId;
        Status = RmaStatus.Requested;
    }

    public void Approve() => ChangeStatus(RmaStatus.Approved);

    public void Reject(string? note)
    {
        ChangeStatus(RmaStatus.Rejected);
        ResolutionNote = note;
    }

    public void Cancel(string? note)
    {
        ChangeStatus(RmaStatus.Cancelled);
        ResolutionNote = note;
    }

    public void Complete(string? note, decimal? refundAmount)
    {
        ChangeStatus(RmaStatus.Completed);
        ResolutionNote = note;
        RefundAmount = refundAmount;
    }

    private void ChangeStatus(RmaStatus status)
    {
        StateMachine.EnsureTransition(Transitions, Status, status, "Rma");
        Status = status;
        SetUpdated();
    }
}
