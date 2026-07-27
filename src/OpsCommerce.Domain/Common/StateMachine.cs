namespace OpsCommerce.Domain.Common;

/// <summary>
/// A small, dependency-free state machine helper.
///
/// Each entity that has a lifecycle (Order, Payment, Fulfillment, Transfer,
/// Production, RMA) declares a static map of allowed transitions
/// (<c>from → to[]</c>). Every status-changing method calls
/// <see cref="EnsureTransition"/> first, so an invalid jump — for example
/// shipping an order that was never paid — fails with a
/// <see cref="BusinessRuleException"/> and becomes an HTTP 422 at the API.
///
/// Moving to the same status is treated as a no-op, which makes retried
/// requests safe.
/// </summary>
public static class StateMachine
{
    public static bool CanTransition<TStatus>(
        IReadOnlyDictionary<TStatus, TStatus[]> map, TStatus from, TStatus to)
        where TStatus : struct, Enum
    {
        if (EqualityComparer<TStatus>.Default.Equals(from, to))
            return true;

        return map.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;
    }

    public static void EnsureTransition<TStatus>(
        IReadOnlyDictionary<TStatus, TStatus[]> map, TStatus from, TStatus to, string entity)
        where TStatus : struct, Enum
    {
        if (CanTransition(map, from, to))
            return;

        throw new BusinessRuleException(
            $"Invalid {entity} status transition: {from} → {to}.",
            $"{entity.ToUpperInvariant()}_INVALID_TRANSITION");
    }
}
