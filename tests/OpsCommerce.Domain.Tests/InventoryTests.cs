using FluentAssertions;
using OpsCommerce.Domain.Common;
using OpsCommerce.Domain.Inventory;

namespace OpsCommerce.Domain.Tests;

// ATP inventory rules and TTL reservations.
public class InventoryTests
{
    private static InventoryItem NewItem(int onHand = 10) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), onHand);

    [Fact]
    public void Reserving_Reduces_Available_To_Promise()
    {
        var item = NewItem(10);
        item.Reserve(4);
        item.QuantityReserved.Should().Be(4);
        item.AvailableToPromise.Should().Be(6);
    }

    [Fact]
    public void Cannot_Reserve_Beyond_ATP()
    {
        var item = NewItem(10);
        item.Reserve(7);
        var act = () => item.Reserve(4); // only 3 left
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("INVENTORY_INSUFFICIENT");
    }

    [Fact]
    public void Shipping_Consumes_Both_OnHand_And_Reserved()
    {
        var item = NewItem(10);
        item.Reserve(5);
        item.Ship(5);
        item.QuantityOnHand.Should().Be(5);
        item.QuantityReserved.Should().Be(0);
        item.AvailableToPromise.Should().Be(5);
    }

    [Fact]
    public void Cannot_Ship_More_Than_Reserved()
    {
        var item = NewItem(10);
        item.Reserve(2);
        var act = () => item.Ship(3);
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("INVENTORY_SHIP_EXCEEDS_RESERVED");
    }

    [Fact]
    public void Stock_Count_Cannot_Go_Below_Reserved()
    {
        var item = NewItem(10);
        item.Reserve(6);
        var act = () => item.AdjustOnHand(4);
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("INVENTORY_ADJUST_BELOW_RESERVED");
    }

    [Fact]
    public void Reservation_Expiry_And_Commit_Lifecycle()
    {
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var r = new Reservation(Guid.NewGuid(), Guid.NewGuid(), null, 3, baseTime.AddMinutes(15));

        r.IsExpired(baseTime.AddMinutes(10)).Should().BeFalse();
        r.IsExpired(baseTime.AddMinutes(20)).Should().BeTrue();

        r.Commit();
        r.Status.Should().Be(ReservationStatus.Committed);

        var act = () => r.Release(); // no longer active
        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("RESERVATION_NOT_ACTIVE");
    }
}
