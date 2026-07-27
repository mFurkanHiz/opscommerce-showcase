# Code highlights

Commented excerpts from the private service layer — the parts that show *how* the system stays correct under real-world conditions. The complete domain layer is in [`src/`](../src/OpsCommerce.Domain) and runs in CI; these excerpts are read-only context around it.

| Highlight | What it demonstrates |
|---|---|
| [Atomic stock reservation](atomic-reservation.md) | overselling protection with one conditional UPDATE — no locks, no retry loops |
| [Payment result claim](payment-claim.md) | closing a double-confirmation race the same way |
| [Tenant scoping](tenant-scoping.md) | one small helper, applied uniformly to queries and commands |
| [Audit trail writer](audit-trail.md) | logging that can never lie, because it commits with the operation |
| [Reservation expiry worker](expiry-worker.md) | the background sweep that guarantees stock is never locked forever |
