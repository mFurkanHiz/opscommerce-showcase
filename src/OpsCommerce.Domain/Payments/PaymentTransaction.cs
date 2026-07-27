using OpsCommerce.Domain.Common;

namespace OpsCommerce.Domain.Payments;

/// <summary>
/// One payment attempt against an order. With deposits enabled, a single
/// order can have several transactions (deposit first, balance later).
///
/// Every Mark* method is guarded by the transition map, so a payment can
/// never be "succeeded" twice or refunded before it succeeded. The service
/// layer adds one more protection on top: the Pending → final transition
/// is claimed with a single conditional UPDATE, so two concurrent
/// confirmations cannot both win.
/// </summary>
public class PaymentTransaction : AuditableEntity
{
    public Guid OrderId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string PaymentMethod { get; private set; } = null!;
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = "TRY";
    public PaymentStatus Status { get; private set; }
    public string TransactionId { get; private set; } = null!;
    public string? FailureReason { get; private set; }

    private PaymentTransaction() { }

    public PaymentTransaction(
        Guid orderId,
        string provider,
        string paymentMethod,
        decimal amount,
        string currencyCode,
        PaymentStatus status,
        string transactionId,
        string? failureReason = null)
    {
        OrderId = orderId;
        Provider = provider;
        PaymentMethod = paymentMethod;
        Amount = amount;
        CurrencyCode = currencyCode;
        Status = status;
        TransactionId = transactionId;
        FailureReason = failureReason;
    }

    private static readonly IReadOnlyDictionary<PaymentStatus, PaymentStatus[]> Transitions =
        new Dictionary<PaymentStatus, PaymentStatus[]>
        {
            [PaymentStatus.Pending]    = [PaymentStatus.Authorized, PaymentStatus.Succeeded, PaymentStatus.Failed, PaymentStatus.Cancelled],
            [PaymentStatus.Authorized] = [PaymentStatus.Succeeded, PaymentStatus.Failed, PaymentStatus.Cancelled],
            [PaymentStatus.Succeeded]  = [PaymentStatus.Refunded],
            [PaymentStatus.Failed]     = [],
            [PaymentStatus.Cancelled]  = [],
            [PaymentStatus.Refunded]   = [],
        };

    public void MarkSucceeded(string transactionId)
    {
        StateMachine.EnsureTransition(Transitions, Status, PaymentStatus.Succeeded, "Payment");
        TransactionId = transactionId;
        Status = PaymentStatus.Succeeded;
        FailureReason = null;
        SetUpdated();
    }

    public void MarkFailed(string? failureReason)
    {
        StateMachine.EnsureTransition(Transitions, Status, PaymentStatus.Failed, "Payment");
        Status = PaymentStatus.Failed;
        FailureReason = failureReason;
        SetUpdated();
    }

    public void MarkCancelled(string? reason = null)
    {
        StateMachine.EnsureTransition(Transitions, Status, PaymentStatus.Cancelled, "Payment");
        Status = PaymentStatus.Cancelled;
        FailureReason = reason;
        SetUpdated();
    }

    public void MarkRefunded(string? reason = null)
    {
        StateMachine.EnsureTransition(Transitions, Status, PaymentStatus.Refunded, "Payment");
        Status = PaymentStatus.Refunded;
        FailureReason = reason;
        SetUpdated();
    }
}
