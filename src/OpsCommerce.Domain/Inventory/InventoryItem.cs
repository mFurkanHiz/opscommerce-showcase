using OpsCommerce.Domain.Common;

namespace OpsCommerce.Domain.Inventory;

/// <summary>
/// Stock of one product at one warehouse location.
///
/// ATP (Available-To-Promise) = OnHand − Reserved. All domain methods are
/// guarded, and the concurrency-critical path (reserving) is additionally
/// done in the service layer with a single conditional UPDATE
/// (<c>WHERE OnHand - Reserved &gt;= qty</c>), so two parallel checkouts
/// can never oversell the same unit.
/// </summary>
public class InventoryItem : AuditableEntity
{
    public Guid CompanyId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid ProductId { get; private set; }
    public int QuantityOnHand { get; private set; }
    public int QuantityReserved { get; private set; }

    public int AvailableToPromise => QuantityOnHand - QuantityReserved;

    private InventoryItem() { }

    public InventoryItem(Guid companyId, Guid locationId, Guid productId, int quantityOnHand = 0)
    {
        if (quantityOnHand < 0)
            throw new BusinessRuleException("Quantity on hand cannot be negative.", "INVENTORY_NEGATIVE");

        CompanyId = companyId;
        LocationId = locationId;
        ProductId = productId;
        QuantityOnHand = quantityOnHand;
        QuantityReserved = 0;
    }

    /// <summary>Physical stock intake (production output, supply, transfer arrival).</summary>
    public void Receive(int quantity)
    {
        RequirePositive(quantity);
        QuantityOnHand += quantity;
        SetUpdated();
    }

    /// <summary>Reserves from ATP (single-context path; concurrent path uses an atomic UPDATE).</summary>
    public void Reserve(int quantity)
    {
        RequirePositive(quantity);
        if (AvailableToPromise < quantity)
            throw new BusinessRuleException("Insufficient available-to-promise stock.", "INVENTORY_INSUFFICIENT");

        QuantityReserved += quantity;
        SetUpdated();
    }

    /// <summary>Gives a reservation back (cancel or TTL expiry).</summary>
    public void ReleaseReservation(int quantity)
    {
        RequirePositive(quantity);
        QuantityReserved = Math.Max(0, QuantityReserved - quantity);
        SetUpdated();
    }

    /// <summary>Ships reserved stock: both reserved and physical quantities go down.</summary>
    public void Ship(int quantity)
    {
        RequirePositive(quantity);
        if (quantity > QuantityReserved)
            throw new BusinessRuleException("Cannot ship more than reserved.", "INVENTORY_SHIP_EXCEEDS_RESERVED");
        if (quantity > QuantityOnHand)
            throw new BusinessRuleException("Cannot ship more than on hand.", "INVENTORY_SHIP_EXCEEDS_ONHAND");

        QuantityOnHand -= quantity;
        QuantityReserved -= quantity;
        SetUpdated();
    }

    /// <summary>Stock-count correction — can never go below the reserved amount.</summary>
    public void AdjustOnHand(int newOnHand)
    {
        if (newOnHand < 0)
            throw new BusinessRuleException("Quantity on hand cannot be negative.", "INVENTORY_NEGATIVE");
        if (newOnHand < QuantityReserved)
            throw new BusinessRuleException("On hand cannot be below reserved quantity.", "INVENTORY_ADJUST_BELOW_RESERVED");

        QuantityOnHand = newOnHand;
        SetUpdated();
    }

    private static void RequirePositive(int quantity)
    {
        if (quantity <= 0)
            throw new BusinessRuleException("Quantity must be positive.", "INVENTORY_QTY_INVALID");
    }
}
