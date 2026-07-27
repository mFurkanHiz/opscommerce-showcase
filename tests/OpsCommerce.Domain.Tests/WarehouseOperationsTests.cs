using FluentAssertions;
using OpsCommerce.Domain.Common;
using OpsCommerce.Domain.Fulfillment;
using OpsCommerce.Domain.Inventory;
using OpsCommerce.Domain.Production;

namespace OpsCommerce.Domain.Tests;

// Transfers, production orders and courier delivery flows.
public class WarehouseOperationsTests
{
    // ── Stock transfers ──────────────────────────────────────────────────────

    private static StockTransfer NewTransfer() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, null);

    [Fact]
    public void Transfer_To_The_Same_Location_Is_Rejected()
    {
        var loc = Guid.NewGuid();
        var act = () => new StockTransfer(Guid.NewGuid(), loc, loc, Guid.NewGuid(), 5, null);
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("TRANSFER_SAME_LOCATION");
    }

    [Fact]
    public void Transfer_Lifecycle_Draft_Dispatch_Complete()
    {
        var t = NewTransfer();
        t.Dispatch();
        t.Complete();
        t.Status.Should().Be(StockTransferStatus.Completed);
    }

    [Fact]
    public void A_Transfer_On_The_Road_Cannot_Be_Cancelled()
    {
        var t = NewTransfer();
        t.Dispatch();
        var act = () => t.Cancel();
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("STOCKTRANSFER_INVALID_TRANSITION");
    }

    // ── Production orders ────────────────────────────────────────────────────

    [Fact]
    public void Production_Lifecycle_Draft_Start_Complete()
    {
        var p = new ProductionOrder(Guid.NewGuid(), Guid.NewGuid(), 10, Guid.NewGuid(), null);
        p.Start();
        p.Complete();
        p.Status.Should().Be(ProductionStatus.Completed);
    }

    [Fact]
    public void Production_Cannot_Complete_Before_It_Started()
    {
        var p = new ProductionOrder(Guid.NewGuid(), Guid.NewGuid(), 10, Guid.NewGuid(), null);
        var act = () => p.Complete();
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("PRODUCTION_INVALID_TRANSITION");
    }

    // ── Courier deliveries ───────────────────────────────────────────────────

    private static FulfillmentOperation NewDelivery() =>
        new(Guid.NewGuid(), FulfillmentType.OrderShipment, null, null, Guid.NewGuid(), Guid.Empty, null, null);

    [Fact]
    public void Assigning_A_Courier_Moves_The_Delivery_To_Assigned()
    {
        var f = NewDelivery();
        var courier = Guid.NewGuid();

        f.AssignCourier(courier);

        f.AssignedCourierId.Should().Be(courier);
        f.Status.Should().Be(FulfillmentStatus.Assigned);
    }

    [Fact]
    public void Courier_Steps_Must_Happen_In_Order()
    {
        var f = NewDelivery();
        f.AssignCourier(Guid.NewGuid());
        f.ChangeStatus(FulfillmentStatus.PickedUp);
        f.ChangeStatus(FulfillmentStatus.InTransit);

        // Cannot complete without arriving first.
        var act = () => f.ChangeStatus(FulfillmentStatus.Completed);
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("FULFILLMENT_INVALID_TRANSITION");

        f.ChangeStatus(FulfillmentStatus.Arrived);
        f.ChangeStatus(FulfillmentStatus.Completed);
        f.Status.Should().Be(FulfillmentStatus.Completed);
    }
}
