using OpsCommerce.Domain.Common;

namespace OpsCommerce.Domain.Inventory;

public enum ReservationStatus
{
    Active = 1,
    Committed = 2,
    Released = 3,
    Expired = 4
}

/// <summary>
/// A stock reservation with a time-to-live. When the order is paid the
/// reservation is committed (shipped); if it is cancelled or the TTL runs
/// out, a background worker releases the stock so it cannot stay locked
/// forever. <see cref="IsExpired"/> takes the clock as a parameter to keep
/// the rule fully testable.
/// </summary>
public class Reservation : AuditableEntity
{
    public Guid CompanyId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public Guid? OrderId { get; private set; }
    public int Quantity { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public ReservationStatus Status { get; private set; }

    private Reservation() { }

    public Reservation(Guid companyId, Guid inventoryItemId, Guid? orderId, int quantity, DateTime expiresAtUtc)
    {
        if (quantity <= 0)
            throw new BusinessRuleException("Reservation quantity must be positive.", "RESERVATION_QTY_INVALID");

        CompanyId = companyId;
        InventoryItemId = inventoryItemId;
        OrderId = orderId;
        Quantity = quantity;
        ExpiresAtUtc = expiresAtUtc;
        Status = ReservationStatus.Active;
    }

    public bool IsExpired(DateTime nowUtc) => Status == ReservationStatus.Active && nowUtc >= ExpiresAtUtc;

    public void Commit()
    {
        EnsureActive();
        Status = ReservationStatus.Committed;
        SetUpdated();
    }

    public void Release()
    {
        EnsureActive();
        Status = ReservationStatus.Released;
        SetUpdated();
    }

    public void MarkExpired()
    {
        EnsureActive();
        Status = ReservationStatus.Expired;
        SetUpdated();
    }

    private void EnsureActive()
    {
        if (Status != ReservationStatus.Active)
            throw new BusinessRuleException($"Reservation is not active (current: {Status}).", "RESERVATION_NOT_ACTIVE");
    }
}
