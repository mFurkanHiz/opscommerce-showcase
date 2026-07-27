using OpsCommerce.Domain.Common;

namespace OpsCommerce.Domain.Orders;

/// <summary>
/// A customer order. The price and the currency are always resolved
/// server-side from the catalog — client-sent prices are ignored.
///
/// Payment is flexible: an order can be paid in full, or with a deposit
/// first and the balance later. <see cref="RegisterPayment"/> moves the
/// order through PartiallyPaid → Paid based on the amounts, and the
/// guarded state machine rejects every invalid jump.
/// </summary>
public class Order : AuditableEntity
{
    public Guid CompanyId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public string? GuestToken { get; private set; }
    public string OrderNumber { get; private set; } = null!;
    public string CurrencyCode { get; private set; } = "TRY";
    public decimal TotalAmount { get; private set; }
    public OrderStatus Status { get; private set; }

    // Deposit / partial payment
    public decimal AmountPaid { get; private set; }
    public decimal? DepositAmount { get; private set; }
    public decimal OutstandingAmount => TotalAmount - AmountPaid;
    public bool IsDeposit => DepositAmount.HasValue;

    public ICollection<OrderItem> Items { get; private set; } = new List<OrderItem>();

    private Order() { }

    public Order(Guid companyId, Guid? customerId, string? guestToken, string orderNumber, string currencyCode, decimal totalAmount)
    {
        CompanyId = companyId;
        CustomerId = customerId;
        GuestToken = guestToken;
        OrderNumber = orderNumber;
        CurrencyCode = currencyCode;
        TotalAmount = totalAmount;
        Status = OrderStatus.PendingPayment;
    }

    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Draft]           = [OrderStatus.PendingPayment, OrderStatus.Cancelled],
            [OrderStatus.PendingPayment]  = [OrderStatus.PartiallyPaid, OrderStatus.Paid, OrderStatus.PaymentFailed, OrderStatus.Cancelled],
            // A failed payment is not a cancellation: the customer can retry.
            [OrderStatus.PaymentFailed]   = [OrderStatus.Paid, OrderStatus.PartiallyPaid, OrderStatus.PendingPayment, OrderStatus.Cancelled],
            [OrderStatus.PartiallyPaid]   = [OrderStatus.Paid, OrderStatus.RefundRequested, OrderStatus.Cancelled],
            [OrderStatus.Paid]            = [OrderStatus.Processing, OrderStatus.RefundRequested, OrderStatus.Cancelled],
            [OrderStatus.Processing]      = [OrderStatus.Shipped, OrderStatus.RefundRequested, OrderStatus.Cancelled],
            [OrderStatus.Shipped]         = [OrderStatus.Delivered, OrderStatus.RefundRequested],
            [OrderStatus.Delivered]       = [OrderStatus.RefundRequested],
            [OrderStatus.RefundRequested] = [OrderStatus.RefundCompleted, OrderStatus.Cancelled],
            [OrderStatus.RefundCompleted] = [],
            [OrderStatus.Cancelled]       = [],
        };

    /// <summary>Guarded transition — an invalid status jump throws (HTTP 422 at the API).</summary>
    public void ChangeStatus(OrderStatus status)
    {
        StateMachine.EnsureTransition(Transitions, Status, status, "Order");
        if (Status == status) return;
        Status = status;
        SetUpdated();
    }

    /// <summary>Sets the up-front (deposit) amount. Only allowed on a fresh, unpaid order.</summary>
    public void SetDepositPlan(decimal depositAmount)
    {
        if (Status != OrderStatus.PendingPayment || AmountPaid != 0)
            throw new BusinessRuleException("Deposit can only be set on a new unpaid order.", "DEPOSIT_STATE_INVALID");
        if (depositAmount <= 0 || depositAmount >= TotalAmount)
            throw new BusinessRuleException("Deposit must be positive and less than the total amount.", "DEPOSIT_AMOUNT_INVALID");

        DepositAmount = depositAmount;
        SetUpdated();
    }

    /// <summary>
    /// Records a partial or full payment. Overpaying is rejected;
    /// the status moves to PartiallyPaid or Paid depending on the balance.
    /// </summary>
    public void RegisterPayment(decimal amount)
    {
        if (amount <= 0)
            throw new BusinessRuleException("Payment amount must be positive.", "PAYMENT_AMOUNT_INVALID");
        if (AmountPaid + amount > TotalAmount)
            throw new BusinessRuleException("Payment exceeds the order outstanding balance.", "PAYMENT_OVERPAY");

        AmountPaid += amount;
        ChangeStatus(AmountPaid >= TotalAmount ? OrderStatus.Paid : OrderStatus.PartiallyPaid);
    }

    /// <summary>
    /// Recalculates the total after an order line was customized.
    /// Only allowed before payment. If a deposit plan exists, it is scaled
    /// by the same ratio so it never goes stale.
    /// </summary>
    public void RecalculateTotal(decimal newTotal)
    {
        if (Status != OrderStatus.PendingPayment)
            throw new BusinessRuleException("Order total can only change before payment (PendingPayment).", "ORDER_TOTAL_LOCKED");
        if (newTotal < 0)
            throw new BusinessRuleException("Order total cannot be negative.", "ORDER_TOTAL_INVALID");

        if (DepositAmount.HasValue && TotalAmount > 0)
        {
            var scaled = Math.Round(DepositAmount.Value * newTotal / TotalAmount, 2, MidpointRounding.AwayFromZero);
            DepositAmount = (scaled > 0 && scaled < newTotal) ? scaled : null;
        }

        TotalAmount = newTotal;
        SetUpdated();
    }
}
