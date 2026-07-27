namespace OpsCommerce.Domain.Common;

/// <summary>
/// Base class for entities that track their lifecycle.
/// Deleting is always a soft delete: the row is kept for auditing,
/// and a global query filter hides it from normal queries.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; protected set; }
    public bool IsActive { get; protected set; } = true;
    public bool IsDeleted { get; protected set; }

    public void SetUpdated()
    {
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
