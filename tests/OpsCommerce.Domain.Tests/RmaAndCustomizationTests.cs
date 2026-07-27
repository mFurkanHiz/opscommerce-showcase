using FluentAssertions;
using OpsCommerce.Domain.Common;
using OpsCommerce.Domain.Orders;
using OpsCommerce.Domain.Rma;

namespace OpsCommerce.Domain.Tests;

// Returns / exchanges / repairs, and per-order line customization.
public class RmaAndCustomizationTests
{
    private static RmaRequest NewRma() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "guest-1", RmaType.Return, 2, "damaged on arrival", Guid.NewGuid());

    [Fact]
    public void Rma_Lifecycle_Requested_Approved_Completed()
    {
        var r = NewRma();
        r.Approve();
        r.Complete("refund issued", 100m);

        r.Status.Should().Be(RmaStatus.Completed);
        r.RefundAmount.Should().Be(100m);
    }

    [Fact]
    public void Rma_Cannot_Complete_Without_Approval()
    {
        var r = NewRma();
        var act = () => r.Complete(null, null);
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("RMA_INVALID_TRANSITION");
    }

    [Fact]
    public void Rma_Requires_A_Reason_And_A_Positive_Quantity()
    {
        var act = () => new RmaRequest(Guid.NewGuid(), Guid.NewGuid(), null, null, null, RmaType.Return, 0, "x", null);
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("RMA_QTY_INVALID");
    }

    // ── Per-order customization ──────────────────────────────────────────────

    private static OrderItem NewItem() =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, "Sofa", "SKU", 1000m, 1, "TRY");

    [Fact]
    public void Customizing_Updates_Only_The_Provided_Fields()
    {
        var i = NewItem();
        i.Customize("Custom Sofa", 1500m, 2, "walnut finish, 3m custom size");

        i.ItemName.Should().Be("Custom Sofa");
        i.TotalPrice.Should().Be(3000m);
        i.CustomizationNote.Should().Be("walnut finish, 3m custom size");
    }

    [Fact]
    public void Null_Fields_Keep_Their_Current_Values()
    {
        var i = NewItem();
        i.Customize(null, null, null, "note only");

        i.ItemName.Should().Be("Sofa");
        i.UnitPrice.Should().Be(1000m);
        i.CustomizationNote.Should().Be("note only");
    }

    [Fact]
    public void A_Negative_Price_Is_Rejected()
    {
        var i = NewItem();
        var act = () => i.Customize(null, -5m, null, null);
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("ITEM_PRICE_INVALID");
    }
}
