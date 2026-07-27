# Audit trail writer

**Problem.** Operational logs are only useful if they are *true*. A logger that writes independently of the operation can record actions that were rolled back — or miss actions that happened.

**Solution.** The writer does not save anything. It only **adds** the entry to the current EF unit of work; the entry is persisted by the same `SaveChanges` (and the same transaction) as the operation itself. Operation fails → no log line. Operation commits → the log line is guaranteed.

```csharp
public interface IActivityLogger
{
    // Adds a log row to the current unit of work. Deliberately synchronous
    // and side-effect-free until the caller saves.
    void Log(string entityType, Guid entityId, string action, string? details = null,
        Guid? companyId = null, Guid? orderId = null);
}

public sealed class ActivityLogger(
    OpsCommerceDbContext dbContext, ICurrentUserService currentUser) : IActivityLogger
{
    public void Log(string entityType, Guid entityId, string action, string? details = null,
        Guid? companyId = null, Guid? orderId = null)
    {
        var actorLabel = currentUser.Email
            ?? (currentUser.UserId.HasValue
                ? $"user:{currentUser.UserId.Value.ToString()[..8]}"
                : "guest");

        dbContext.ActivityLogs.Add(new ActivityLog(
            entityType, entityId, action, details,
            companyId, orderId, currentUser.UserId, actorLabel));
    }
}
```

Instrumentation is one line at each consequential point, always **before** the service's own `SaveChanges`:

```csharp
order.RegisterPayment(payment.Amount);
activity.Log("Order", order.Id, "PaymentReceived",
    $"paid {order.AmountPaid}/{order.TotalAmount} → {order.Status}",
    order.CompanyId, order.Id);
// … one SaveChanges commits the payment, the order update AND the log line together
```

The `orderId` column is the correlation key: querying `/api/activity?orderId=…` replays an order's whole life across entities — creation, failed and retried payments, courier steps, refunds. Access is owner-scoped (the order's company, the order's customer, or an admin).

Real output for one order:

```
Order    · Created          · ORD-2607-269816 · 45,999.00 TRY · 1 item
Payment  · Created          · 45,999.00 TRY pending
Payment  · Failed           · simulated failure
Order    · PaymentFailed
Payment  · Created          · 45,999.00 TRY pending
Payment  · Succeeded        · 45,999.00 TRY collected
Order    · PaymentReceived  · paid 45,999.00/45,999.00 → Paid
Order    · StatusChanged    · Paid → Processing
Order    · StatusChanged    · Processing → Shipped
```

Background work logs too: when the expiry worker releases a timed-out reservation, it writes the entry itself with `ActorLabel = "system"` — so even automated actions are attributable.
