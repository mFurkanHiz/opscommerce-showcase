using OpsCommerce.Domain.Common;

namespace OpsCommerce.Domain.Production;

public enum ProductionStatus
{
    Draft = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>
/// A production order that is not tied to any sale — manufacturing for
/// stock. Lifecycle: Draft → InProgress → Completed. When it completes,
/// the service layer receives the produced quantity into the target
/// warehouse, so production directly feeds inventory.
/// </summary>
public class ProductionOrder : AuditableEntity
{
    public Guid CompanyId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public Guid TargetLocationId { get; private set; }
    public ProductionStatus Status { get; private set; }
    public string? Notes { get; private set; }

    private static readonly IReadOnlyDictionary<ProductionStatus, ProductionStatus[]> Transitions =
        new Dictionary<ProductionStatus, ProductionStatus[]>
        {
            [ProductionStatus.Draft]      = [ProductionStatus.InProgress, ProductionStatus.Cancelled],
            [ProductionStatus.InProgress] = [ProductionStatus.Completed, ProductionStatus.Cancelled],
            [ProductionStatus.Completed]  = [],
            [ProductionStatus.Cancelled]  = [],
        };

    private ProductionOrder() { }

    public ProductionOrder(Guid companyId, Guid productId, int quantity, Guid targetLocationId, string? notes)
    {
        if (quantity <= 0)
            throw new BusinessRuleException("Production quantity must be positive.", "PRODUCTION_QTY_INVALID");

        CompanyId = companyId;
        ProductId = productId;
        Quantity = quantity;
        TargetLocationId = targetLocationId;
        Notes = notes;
        Status = ProductionStatus.Draft;
    }

    public void Start() => ChangeStatus(ProductionStatus.InProgress);
    public void Complete() => ChangeStatus(ProductionStatus.Completed);
    public void Cancel() => ChangeStatus(ProductionStatus.Cancelled);

    private void ChangeStatus(ProductionStatus status)
    {
        StateMachine.EnsureTransition(Transitions, Status, status, "Production");
        Status = status;
        SetUpdated();
    }
}
