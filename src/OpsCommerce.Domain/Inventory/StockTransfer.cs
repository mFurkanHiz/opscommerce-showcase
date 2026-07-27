using OpsCommerce.Domain.Common;

namespace OpsCommerce.Domain.Inventory;

public enum StockTransferStatus
{
    Draft = 1,
    Dispatched = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>
/// Warehouse-to-warehouse stock movement with a guarded lifecycle:
/// Draft → Dispatched → Completed (Draft can also be Cancelled).
///
/// The service layer maps each step to a stock effect:
/// create = reserve at source, dispatch = ship from source,
/// complete = receive at destination, cancel = release the reservation.
/// A transfer that is already on the road cannot be cancelled.
/// </summary>
public class StockTransfer : AuditableEntity
{
    public Guid CompanyId { get; private set; }
    public Guid SourceLocationId { get; private set; }
    public Guid DestinationLocationId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public StockTransferStatus Status { get; private set; }
    public string? Notes { get; private set; }

    private static readonly IReadOnlyDictionary<StockTransferStatus, StockTransferStatus[]> Transitions =
        new Dictionary<StockTransferStatus, StockTransferStatus[]>
        {
            [StockTransferStatus.Draft]      = [StockTransferStatus.Dispatched, StockTransferStatus.Cancelled],
            [StockTransferStatus.Dispatched] = [StockTransferStatus.Completed],
            [StockTransferStatus.Completed]  = [],
            [StockTransferStatus.Cancelled]  = [],
        };

    private StockTransfer() { }

    public StockTransfer(Guid companyId, Guid sourceLocationId, Guid destinationLocationId, Guid productId, int quantity, string? notes)
    {
        if (sourceLocationId == destinationLocationId)
            throw new BusinessRuleException("Source and destination locations must differ.", "TRANSFER_SAME_LOCATION");
        if (quantity <= 0)
            throw new BusinessRuleException("Transfer quantity must be positive.", "TRANSFER_QTY_INVALID");

        CompanyId = companyId;
        SourceLocationId = sourceLocationId;
        DestinationLocationId = destinationLocationId;
        ProductId = productId;
        Quantity = quantity;
        Notes = notes;
        Status = StockTransferStatus.Draft;
    }

    public void Dispatch() => ChangeStatus(StockTransferStatus.Dispatched);
    public void Complete() => ChangeStatus(StockTransferStatus.Completed);
    public void Cancel() => ChangeStatus(StockTransferStatus.Cancelled);

    private void ChangeStatus(StockTransferStatus status)
    {
        StateMachine.EnsureTransition(Transitions, Status, status, "StockTransfer");
        Status = status;
        SetUpdated();
    }
}
