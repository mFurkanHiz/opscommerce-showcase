using OpsCommerce.Domain.Common;

namespace OpsCommerce.Domain.Activity;

/// <summary>
/// One row of the operational audit trail. Every meaningful action in the
/// system — order created, payment failed, courier picked up, stock
/// reserved, RMA completed — is written here and linked to the related
/// order and the acting user, so any order can be replayed as a full
/// step-by-step story.
///
/// The writer only ADDS the row to the current unit of work; it is saved
/// by the same SaveChanges as the operation itself, so a failed operation
/// never leaves a misleading log entry behind.
/// </summary>
public class ActivityLog : AuditableEntity
{
    public Guid? CompanyId { get; private set; }
    public Guid? OrderId { get; private set; }
    public string EntityType { get; private set; } = null!;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = null!;
    public string? Details { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorLabel { get; private set; } = null!;

    private ActivityLog() { }

    public ActivityLog(
        string entityType, Guid entityId, string action, string? details,
        Guid? companyId, Guid? orderId, Guid? actorUserId, string actorLabel)
    {
        EntityType = entityType;
        EntityId = entityId;
        Action = action;
        Details = details;
        CompanyId = companyId;
        OrderId = orderId;
        ActorUserId = actorUserId;
        ActorLabel = actorLabel;
    }
}
