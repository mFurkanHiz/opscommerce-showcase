using OpsCommerce.Domain.Common;

namespace OpsCommerce.Domain.Catalog;

/// <summary>
/// A sellable product owned by a company (tenant).
///
/// A seller can allow deposit-based selling per product: the customer pays
/// an up-front fraction (for example 30%) at checkout and the balance
/// later. The rate is validated here, and the checkout flow computes the
/// deposit amount server-side from this configuration — never from
/// client input.
/// </summary>
public class Product : AuditableEntity
{
    public Guid CompanyId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Sku { get; private set; } = null!;
    public string? Description { get; private set; }
    public decimal BasePrice { get; private set; }
    public string Currency { get; private set; } = null!;
    public bool IsPublished { get; private set; }
    public VisibilityLevel VisibilityLevel { get; private set; } = VisibilityLevel.Public;

    // Deposit selling (per product, seller-controlled)
    public bool DepositEnabled { get; private set; }
    public decimal? DepositRate { get; private set; }

    private Product() { }

    public Product(
        Guid companyId,
        Guid? categoryId,
        string name,
        string sku,
        string? description,
        decimal basePrice,
        string currency,
        bool isPublished,
        VisibilityLevel visibilityLevel = VisibilityLevel.Public)
    {
        CompanyId = companyId;
        CategoryId = categoryId;
        Name = name;
        Sku = sku;
        Description = description;
        BasePrice = basePrice;
        Currency = currency;
        IsPublished = isPublished;
        VisibilityLevel = visibilityLevel;
    }

    public void UpdateDetails(string name, string sku, string? description, decimal basePrice, string currency, bool isPublished, VisibilityLevel visibilityLevel = VisibilityLevel.Public)
    {
        Name = name;
        Sku = sku;
        Description = description;
        BasePrice = basePrice;
        Currency = currency;
        IsPublished = isPublished;
        VisibilityLevel = visibilityLevel;
        SetUpdated();
    }

    /// <summary>Enables or disables deposit selling. The rate must be a fraction between 0 and 1.</summary>
    public void ConfigureDeposit(bool enabled, decimal? rate)
    {
        if (enabled && rate is null or <= 0m or >= 1m)
            throw new BusinessRuleException("Deposit rate must be between 0 and 1 (exclusive).", "DEPOSIT_RATE_INVALID");

        DepositEnabled = enabled;
        DepositRate = enabled ? rate : null;
        SetUpdated();
    }
}
