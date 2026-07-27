# Atomic stock reservation

**Problem.** Two customers race for the last unit. If the service reads the stock, checks it in memory and then writes, both reads can see "1 available" and both writes succeed — the classic read-check-write race, and now you have sold one unit twice.

**Solution.** Push the check *into* the write. EF Core's `ExecuteUpdate` becomes a single conditional SQL UPDATE; the database serializes the racers and exactly one wins.

```csharp
// Reserve `quantity` units of one inventory row — the concurrency-critical core.
// No transaction management here: the caller owns the transaction, so a
// single reservation and a whole-order reservation reuse the same core.
private async Task<Reservation> ReserveCoreAsync(
    Guid inventoryItemId, Guid companyId, Guid? orderId,
    int quantity, int ttlMinutes, CancellationToken ct)
{
    // The check and the write are ONE statement:
    //   UPDATE InventoryItems
    //   SET    QuantityReserved += @qty
    //   WHERE  Id = @id AND QuantityOnHand - QuantityReserved >= @qty
    var affected = await dbContext.InventoryItems
        .Where(x => x.Id == inventoryItemId
                    && (x.QuantityOnHand - x.QuantityReserved) >= quantity)
        .ExecuteUpdateAsync(s => s.SetProperty(
            x => x.QuantityReserved,
            x => x.QuantityReserved + quantity), ct);

    // Zero rows affected = someone else took the stock first.
    if (affected == 0)
        throw new BusinessRuleException(
            "Insufficient available-to-promise stock.", "INVENTORY_INSUFFICIENT");

    var expiresAt = DateTime.UtcNow.AddMinutes(ttlMinutes <= 0 ? 15 : ttlMinutes);
    var reservation = new Reservation(companyId, inventoryItemId, orderId, quantity, expiresAt);
    dbContext.Reservations.Add(reservation);
    await dbContext.SaveChangesAsync(ct);
    return reservation;
}
```

Reserving a whole order wraps the same core in **one transaction over all lines** — if the third line has no stock, the first two reservations roll back too:

```csharp
// All-or-nothing: a half-reserved order would silently lock stock forever.
await using var tx = await dbContext.Database.BeginTransactionAsync(ct);

foreach (var line in orderLines)
{
    // Allocation strategy: the warehouse with the most ATP,
    // restricted to the order's own company.
    var target = await dbContext.InventoryItems.AsNoTracking()
        .Where(x => x.ProductId == line.ProductId
                    && x.CompanyId == order.CompanyId
                    && (x.QuantityOnHand - x.QuantityReserved) >= line.Quantity)
        .OrderByDescending(x => x.QuantityOnHand - x.QuantityReserved)
        .FirstOrDefaultAsync(ct)
        ?? throw new BusinessRuleException(
            $"Insufficient stock for product {line.ProductId}.", "INVENTORY_INSUFFICIENT");

    await ReserveCoreAsync(target.Id, target.CompanyId, order.Id, line.Quantity, ttlMinutes, ct);
}

await tx.CommitAsync(ct);   // any throw above = automatic rollback of every line
```

**Why not a lock / semaphore / distributed lock?** They serialize *all* traffic and add an infrastructure dependency. The conditional UPDATE serializes only the actual conflict, costs nothing when there is no contention, and works on a single plain SQL Server.

**Testing note.** EF's in-memory provider cannot execute this UPDATE, and mocking it would prove nothing — so this path is verified against the live environment (reserve → ATP drops → commit → on-hand drops), while the surrounding rules are unit-tested. See [testing](../docs/testing.md).
