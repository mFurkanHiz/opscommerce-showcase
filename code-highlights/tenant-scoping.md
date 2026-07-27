# Tenant scoping

**Problem.** In a multi-tenant system, "is the caller allowed to call this endpoint?" (role) and "may this caller touch this specific row?" (tenant) are different questions. The second one has to be answered on *every* query and *every* command — and it must be impossible to forget quietly.

**Solution.** One tiny, boring helper — applied uniformly:

```csharp
public static class TenantAccess
{
    // Read gate: platform admins see everything; everyone else only their company.
    public static bool CanAccessCompany(this ICurrentUserService user, Guid companyId)
        => user.IsAdmin || (user.CompanyId.HasValue && user.CompanyId.Value == companyId);

    // Write gate: resolves which company a new record belongs to.
    // Admins may specify a company; everyone else is FORCED to their own claim —
    // a client-sent companyId is never trusted.
    public static Guid ResolveCompanyId(this ICurrentUserService user, Guid requestedCompanyId)
    {
        if (user.IsAdmin)
            return requestedCompanyId != Guid.Empty
                ? requestedCompanyId
                : user.CompanyId ?? throw new BusinessRuleException(
                    "A company id is required for this operation.", "COMPANY_CONTEXT_REQUIRED");

        return user.CompanyId
            ?? throw new BusinessRuleException(
                "A company context is required for this operation.", "COMPANY_CONTEXT_REQUIRED");
    }
}
```

Usage patterns, from real services:

```csharp
// Lists: the filter is part of the query, so a foreign row is never even read.
var isAdmin  = currentUser.IsAdmin;
var myCompany = currentUser.CompanyId;

return await dbContext.StockTransfers.AsNoTracking()
    .Where(x => isAdmin || (myCompany != null && x.CompanyId == myCompany))
    .OrderByDescending(x => x.CreatedAtUtc)
    .Select(/* … */)
    .ToPagedResponseAsync(paging, ct);

// Commands: a foreign row behaves as if it does not exist (404, not 403 —
// the system does not confirm the row's existence to outsiders).
var transfer = await dbContext.StockTransfers.FirstOrDefaultAsync(x => x.Id == id, ct);
if (transfer is null || !currentUser.CanAccessCompany(transfer.CompanyId)) return false;

// Cross-tenant REFERENCES are validated too: you cannot create a production
// order that targets another company's warehouse, even knowing its ID.
var locationOk = await dbContext.Locations.AsNoTracking()
    .AnyAsync(l => l.Id == request.TargetLocationId && l.CompanyId == companyId, ct);
if (!locationOk)
    throw new BusinessRuleException(
        "Target location does not belong to the company.", "LOCATION_COMPANY_MISMATCH");
```

Some resources are person-scoped rather than company-scoped and add a third check on top — a courier only advances **their own** deliveries, a customer only sees **their own** orders and addresses, and a guest proves order ownership with the order's guest token.

All of this was verified live with two tenants: company A's staff get `404` for company B's stock and `422` when referencing company B's warehouse.
