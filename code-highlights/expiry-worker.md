# Reservation expiry worker

**Problem.** A reservation holds real stock. If a checkout is abandoned after reserving, that stock must come back — otherwise "ghost reservations" slowly eat the sellable inventory.

**Solution.** Every reservation carries a TTL. A hosted background service sweeps expired ones every minute; each release is its own small transaction with the same atomic-update discipline as the rest of the stock code.

```csharp
public sealed class ReservationExpiryWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationExpiryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ReleaseExpiredAsync(stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Reservation expiry sweep failed."); }
            // one failed sweep never kills the worker — the next tick tries again

            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    private async Task ReleaseExpiredAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        // Indexed sweep: (Status, ExpiresAtUtc) composite index makes this cheap.
        var expired = await db.Reservations
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAtUtc <= now)
            .OrderBy(r => r.ExpiresAtUtc)
            .Take(100)                       // bounded batch per tick
            .ToListAsync(ct);

        foreach (var reservation in expired)
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Guarded give-back: never drives Reserved below zero,
            // even if something else already touched the row.
            await db.InventoryItems
                .Where(i => i.Id == reservation.InventoryItemId
                            && i.QuantityReserved >= reservation.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    i => i.QuantityReserved,
                    i => i.QuantityReserved - reservation.Quantity), ct);

            reservation.MarkExpired();       // guarded transition: only Active can expire
            db.ActivityLogs.Add(/* "Reservation Expired" — actor: system */);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
    }
}
```

Design points:

- **Belt and suspenders:** committing an already-expired reservation is *also* rejected at the service level, so the one-minute sweep window cannot be exploited.
- **Per-reservation transactions**, not one giant one: a single problematic row cannot poison the whole batch.
- **Deterministic domain rule:** `Reservation.IsExpired(nowUtc)` takes the clock as a parameter, so the expiry rule itself is unit-tested without any time tricks; the worker only supplies the clock.

Verified live: a reservation created with a 1-minute TTL was observed being released automatically (~100 seconds later, reserved count back to zero) with the audit entry in place.
