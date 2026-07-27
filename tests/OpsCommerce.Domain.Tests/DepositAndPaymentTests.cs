using FluentAssertions;
using OpsCommerce.Domain.Catalog;
using OpsCommerce.Domain.Common;
using OpsCommerce.Domain.Orders;
using OpsCommerce.Domain.Payments;

namespace OpsCommerce.Domain.Tests;

// Deposit (down-payment) selling and partial payments.
public class DepositAndPaymentTests
{
    private static Order NewOrder(decimal total = 1000m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, "ORD-DEP", "TRY", total);

    [Fact]
    public void Deposit_Then_Balance_Completes_The_Order()
    {
        var o = NewOrder(1000m);
        o.SetDepositPlan(300m);

        o.RegisterPayment(300m);                        // the deposit
        o.Status.Should().Be(OrderStatus.PartiallyPaid);
        o.OutstandingAmount.Should().Be(700m);

        o.RegisterPayment(700m);                        // the balance
        o.Status.Should().Be(OrderStatus.Paid);
        o.OutstandingAmount.Should().Be(0m);
    }

    [Fact]
    public void Overpayment_Is_Rejected()
    {
        var o = NewOrder(500m);
        var act = () => o.RegisterPayment(600m);
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("PAYMENT_OVERPAY");
    }

    [Fact]
    public void Deposit_Must_Be_Less_Than_The_Total()
    {
        var o = NewOrder(500m);
        var act = () => o.SetDepositPlan(500m);
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("DEPOSIT_AMOUNT_INVALID");
    }

    [Fact]
    public void Product_Deposit_Rate_Must_Be_A_Fraction()
    {
        var p = new Product(Guid.NewGuid(), null, "Sofa", "SKU-1", null, 1000m, "TRY", true);

        var act = () => p.ConfigureDeposit(true, 1.5m);
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("DEPOSIT_RATE_INVALID");

        p.ConfigureDeposit(true, 0.30m);
        p.DepositRate.Should().Be(0.30m);
    }

    [Fact]
    public void Recalculating_The_Total_Scales_The_Deposit()
    {
        var o = NewOrder(1000m);
        o.SetDepositPlan(300m);       // 30%

        o.RecalculateTotal(2000m);    // an order line was customized

        o.TotalAmount.Should().Be(2000m);
        o.DepositAmount.Should().Be(600m);   // the ratio is preserved
    }

    [Fact]
    public void A_Payment_Cannot_Be_Refunded_Before_It_Succeeded()
    {
        var p = new PaymentTransaction(Guid.NewGuid(), "Provider", "Card", 100m, "TRY", PaymentStatus.Pending, "T-1");
        var act = () => p.MarkRefunded();
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("PAYMENT_INVALID_TRANSITION");
    }

    [Fact]
    public void Succeed_Then_Refund_Is_A_Valid_Flow()
    {
        var p = new PaymentTransaction(Guid.NewGuid(), "Provider", "Card", 100m, "TRY", PaymentStatus.Pending, "T-1");
        p.MarkSucceeded("T-2");
        p.MarkRefunded("customer request");
        p.Status.Should().Be(PaymentStatus.Refunded);
    }
}
