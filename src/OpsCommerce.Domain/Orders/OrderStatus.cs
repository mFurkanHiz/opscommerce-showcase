namespace OpsCommerce.Domain.Orders;

public enum OrderStatus
{
    Draft = 1,
    PendingPayment = 2,
    Paid = 3,
    Processing = 4,
    Shipped = 5,
    Delivered = 6,
    Cancelled = 7,
    RefundRequested = 8,
    RefundCompleted = 9,
    /// <summary>A deposit was paid; the remaining balance is still open.</summary>
    PartiallyPaid = 10,
    /// <summary>The payment attempt failed. Unlike Cancelled, the order can be retried.</summary>
    PaymentFailed = 11
}
