# Security policy

## Scope

This repository publishes the **domain layer and its tests** of the OpsCommerce platform, plus written documentation. It contains no credentials, no connection strings and no deployment configuration.

The **public demo** at https://opscommerce-demo.rapidconfigs.com is a throwaway sandbox: simulated payments, generated data, and a full database reset every night. It holds no real customer, order or payment data.

## Reporting a vulnerability

If you find a security issue — in the published code, in the demo deployment, or in the way the demo is isolated — please report it privately to **mertfurkan.hiz@gmail.com** instead of opening a public issue. Please include what you did, what you expected, and what happened. I aim to reply within a few days.

Please do not run automated scanners, load tests or denial-of-service attempts against the demo host: it shares a small server with other services, and the write quotas exist precisely so that ordinary exploration stays possible for everyone.

## How the demo is hardened

The demo build enables a restriction layer that the private deployments do not need:

| Guard | Behaviour |
|---|---|
| Locked role | `PlatformAdmin` cannot be signed into; the tenant and licence administration surface is closed |
| Shared accounts | Demo role accounts are read-only — profile and password changes are rejected (`DEMO_ACCOUNT_READONLY`) |
| Deletes | Blocked outside a small allow-list (own addresses, payment methods, cart items) |
| Administrative writes | Companies, licences, currencies and site settings reject writes |
| Write quota | Per-IP hourly cap on state-changing requests, on top of the normal auth and checkout rate limits |
| Uploads | The platform has no file or image upload endpoint at all — there is no storage abuse vector |
| Reset | The database is dropped, migrated and re-seeded every night |

## Security practices in the codebase

These are implemented in the full platform and documented here for review:

- **Authentication** — ASP.NET Identity with JWT bearer tokens; signing keys are supplied from the environment and the application refuses to start outside development with a placeholder or short key.
- **Authorisation** — role-based policies per endpoint, plus tenant scoping on every company-owned resource; a row belonging to another tenant is answered with `404`, not `403`, so existence is not leaked.
- **Server-authoritative pricing** — prices, totals and payment amounts are recomputed on the server; client-supplied values are ignored.
- **Concurrency safety** — stock reservation and payment settlement are single atomic conditional updates, so a race cannot oversell stock or double-settle a payment.
- **Audit trail** — every state change is written to an activity log inside the same transaction as the change itself, so the log cannot drift from reality.
- **Rate limiting** — fixed-window limits on the authentication endpoints and on the anonymous checkout and payment endpoints.

Details and code excerpts: [`docs/security.md`](docs/security.md) and [`code-highlights/`](code-highlights/).
