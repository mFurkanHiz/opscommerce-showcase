# Payment result claim

**Problem.** A payment confirmation can arrive twice — a double-click, a retried webhook, two tabs. If both confirmations pass a naive `if (payment.Status == Pending)` check, the order's paid amount is registered twice.

**Solution.** The same pattern as the stock reservation: make the status transition itself the gate, with one conditional UPDATE. The first confirmation *claims* the payment; the second affects zero rows and returns quietly.

```csharp
public async Task<PaymentTransactionResponse?> MarkSucceededAsync(
    Guid paymentId, SimulatePaymentResultRequest request, CancellationToken ct)
{
    var transactionId = /* provided or generated */;

    // Atomic claim: Pending → Succeeded in one statement.
    // Two concurrent confirmations cannot both pass this line.
    var claimed = await dbContext.PaymentTransactions
        .Where(x => x.Id == paymentId && x.Status == PaymentStatus.Pending)
        .ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, PaymentStatus.Succeeded)
            .SetProperty(x => x.TransactionId, transactionId)
            .SetProperty(x => x.FailureReason, (string?)null)
            .SetProperty(x => x.UpdatedAtUtc, DateTime.UtcNow), ct);

    if (claimed == 0) return null;   // already finalized by someone else — no double effects

    // Only the winner applies the money effects:
    var payment = await dbContext.PaymentTransactions.AsNoTracking()
        .FirstAsync(x => x.Id == paymentId, ct);
    var order = await dbContext.Orders.FirstOrDefaultAsync(x => x.Id == payment.OrderId, ct);

    if (order is not null)
    {
        // Moves the order to PartiallyPaid or Paid based on the balance;
        // overpayment is rejected inside the domain.
        order.RegisterPayment(payment.Amount);

        // Fulfillment starts only on FULL payment — a deposit alone ships nothing.
        if (order.Status == OrderStatus.Paid)
            dbContext.FulfillmentOperations.Add(/* auto-created delivery */);
    }

    await dbContext.SaveChangesAsync(ct);
    return Map(payment);
}
```

Two details that matter:

- **A failed payment is not a cancellation.** The failure path moves the order to `PaymentFailed` — a retryable state — instead of `Cancelled`. Reports must distinguish "the customer gave up" from "the card declined".
- **Deposit-aware amounts.** Creating a pending payment computes the charge server-side: the deposit amount for the first payment of a deposit order, the outstanding balance afterwards. The client never states an amount.

This is exactly the shape a real PSP integration needs (webhook arrives → claim → effects), which makes swapping the simulator for iyzico/Stripe a contained change.
