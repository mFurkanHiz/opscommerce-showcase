using FluentAssertions;
using OpsCommerce.Domain.Common;
using OpsCommerce.Domain.Orders;

namespace OpsCommerce.Domain.Tests;

// The order state machine: valid flows pass, invalid jumps are rejected with a stable error code.
public class OrderLifecycleTests
{
    private static Order NewOrder(decimal total = 1000m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, "ORD-1", "TRY", total); // starts as PendingPayment

    [Fact]
    public void Valid_Lifecycle_Reaches_Delivered()
    {
        var o = NewOrder();
        o.ChangeStatus(OrderStatus.Paid);
        o.ChangeStatus(OrderStatus.Processing);
        o.ChangeStatus(OrderStatus.Shipped);
        o.ChangeStatus(OrderStatus.Delivered);
        o.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void Cannot_Ship_An_Unpaid_Order()
    {
        var o = NewOrder();
        var act = () => o.ChangeStatus(OrderStatus.Shipped);
        act.Should().Throw<BusinessRuleException>()
           .Which.Code.Should().Be("ORDER_INVALID_TRANSITION");
    }

    [Fact]
    public void Cancelled_Is_Terminal()
    {
        var o = NewOrder();
        o.ChangeStatus(OrderStatus.Cancelled);
        var act = () => o.ChangeStatus(OrderStatus.Paid);
        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Same_Status_Is_An_Idempotent_NoOp()
    {
        var o = NewOrder();
        o.ChangeStatus(OrderStatus.PendingPayment); // no throw, no change
        o.Status.Should().Be(OrderStatus.PendingPayment);
    }

    [Fact]
    public void Failed_Payment_Is_Retryable_Not_Terminal()
    {
        var o = NewOrder(100m);
        o.ChangeStatus(OrderStatus.PaymentFailed);
        o.Status.Should().Be(OrderStatus.PaymentFailed);

        o.RegisterPayment(100m); // the retry succeeds
        o.Status.Should().Be(OrderStatus.Paid);
    }
}
