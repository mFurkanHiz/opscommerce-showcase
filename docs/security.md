# Security

Security here is treated as a **process**, not a checklist: the codebase went through an internal security review, every finding was fixed, and every fix was verified against the live environment with real HTTP calls. Regression tests lock the fixes in place. This document summarizes the main classes of findings and how they are handled today.

## Server-authoritative money

The earliest and most important rule: **the client is never trusted with money.**

- Order prices and currencies come from the catalog at checkout. A request claiming a unit price of `0.01` produces an order at the real price (verified live: sent `0.01`, order stored `45,999.00`).
- Deposit amounts are computed server-side from the product's configured rate.
- Refunds are capped at the amount actually paid; RMA quantities are capped at the ordered quantity.
- Overpayment is rejected at the domain level.

## Authorization: two gates on every path

1. **Role gate** — controllers declare which roles may call an endpoint (warehouse staff cannot approve refunds; customers cannot see the operations panel).
2. **Tenant gate** — services verify the specific row belongs to the caller's company (or, for customers, to the caller personally). A foreign row responds as *not found*, so its existence is not even confirmed.

The tenant gate is uniform across orders, customers, inventory, transfers, production, RMA, fulfillment, payments and the audit trail. This was verified live with two tenants: company A's staff receive `404` for company B's stock and `422` when trying to produce into company B's warehouse.

Cross-tenant *references* are validated too: you cannot create a transfer or production order pointing at another company's warehouse or product, even if you know its ID.

## Ownership beyond tenancy

Some resources belong to a person, not a company:

- A courier can only advance **their own** deliveries.
- A customer sees only their own orders, addresses and payment labels; guests prove order ownership with the order's guest token (RMA, order lookup).
- Order timelines are visible to the order's company, the order's customer, or an admin — nobody else.

## Concurrency as a security property

Race conditions can be abused, so the two money/stock-critical races are closed structurally:

- **Stock**: reservation is one conditional `UPDATE` — two racing checkouts cannot both take the last unit.
- **Payments**: the `Pending → Succeeded/Failed` transition is *claimed* with one conditional `UPDATE`; a double confirmation applies its effects exactly once.

## Platform hardening

- **JWT**: the signing key must come from the environment outside development, with a minimum length — the application refuses to start otherwise (fail-fast beats a silently weak default).
- **Rate limiting**: fixed-window limits on the auth endpoints (brute-force) and per-IP limits on the anonymous checkout/payment endpoints (abuse).
- **Error responses**: internal exception details are logged but never returned; clients get generic messages plus stable, machine-readable error codes.
- **Secrets**: never committed. Configuration files with real values live only on the servers and in CI secrets; the repository carries placeholders. Deployment syncs exclude them.
- **PCI scope**: no card data is stored — saved payment methods keep a label, brand and last-4 only.
- **Auditability**: the activity trail (see [operations](operations.md)) records every consequential action with its actor, atomically with the action itself.

## Honest notes

- The demo environment intentionally uses simple, published demo credentials — it exists to be explored. Production onboarding would replace seeded users entirely.
- Payments are simulated; integrating a real PSP adds its own hardening steps (webhook signature verification, idempotency keys), which the current pending→claim→effects flow is already shaped for.
