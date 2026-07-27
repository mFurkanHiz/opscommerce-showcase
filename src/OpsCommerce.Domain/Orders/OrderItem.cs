using OpsCommerce.Domain.Common;

namespace OpsCommerce.Domain.Orders;

/// <summary>
/// A single order line. Name, code and price are a snapshot taken at
/// checkout, so later catalog changes never affect existing orders.
///
/// A line can be customized per order (custom size, material, price…)
/// without touching the global product — a common need in made-to-order
/// businesses like furniture.
/// </summary>
public class OrderItem : AuditableEntity
{
    public Guid OrderId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? ServiceItemId { get; private set; }
    public string ItemName { get; private set; } = null!;
    public string ItemCode { get; private set; } = null!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public string CurrencyCode { get; private set; } = "TRY";
    public string? CustomizationNote { get; private set; }

    private OrderItem() { }

    public OrderItem(Guid orderId, Guid? productId, Guid? serviceItemId, string itemName, string itemCode, decimal unitPrice, int quantity, string currencyCode)
    {
        OrderId = orderId;
        ProductId = productId;
        ServiceItemId = serviceItemId;
        ItemName = itemName;
        ItemCode = itemCode;
        UnitPrice = unitPrice;
        Quantity = quantity;
        CurrencyCode = currencyCode;
    }

    public decimal TotalPrice => UnitPrice * Quantity;

    /// <summary>Customizes this line only. Null fields keep their current value.</summary>
    public void Customize(string? itemName, decimal? unitPrice, int? quantity, string? customizationNote)
    {
        if (!string.IsNullOrWhiteSpace(itemName))
            ItemName = itemName;
        if (unitPrice.HasValue)
        {
            if (unitPrice.Value < 0)
                throw new BusinessRuleException("Unit price cannot be negative.", "ITEM_PRICE_INVALID");
            UnitPrice = unitPrice.Value;
        }
        if (quantity.HasValue)
        {
            if (quantity.Value <= 0)
                throw new BusinessRuleException("Quantity must be positive.", "ITEM_QTY_INVALID");
            Quantity = quantity.Value;
        }
        if (customizationNote is not null)
            CustomizationNote = customizationNote;

        SetUpdated();
    }
}
