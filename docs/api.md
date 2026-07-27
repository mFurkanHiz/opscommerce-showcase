# API surface

A condensed map of the HTTP API (full Swagger runs with the application). All endpoints return problem-details errors with stable `errorCode`s; write endpoints are role-gated and tenant-scoped as described in [security](security.md).

## Storefront & customer

| Method | Route | Notes |
|---|---|---|
| GET | `/api/products`, `/api/products/{id}` | public catalog (visibility-filtered) |
| POST | `/api/orders/from-cart` | guest or signed-in checkout; **prices resolved server-side**; optional `payDeposit`; rate-limited per IP |
| POST | `/api/payments/simulation` → `…/{id}/success` \| `…/{id}/fail` | simulated payment lifecycle; deposit-aware amounts; atomic claim on the result |
| GET/PUT | `/api/auth/me` | profile + password |
| GET/POST/PUT/DELETE | `/api/me/addresses` | multiple addresses, shipping/billing, default handling |
| GET/POST/DELETE | `/api/me/payment-methods` | label + brand + last-4 only |
| POST | `/api/rma` | return/exchange/repair; owner-verified (customer or guest token) |

## Operations

| Method | Route | Notes |
|---|---|---|
| GET | `/api/dashboard/summary` | management roles; company-scoped KPIs |
| GET | `/api/dashboard/my-work` | role-aware metrics + work queue (warehouse/courier/ops/seller each see their own) |
| GET/PUT | `/api/orders`, `/api/orders/{id}/status` | guarded status transitions |
| PUT | `/api/orders/{id}/items/{itemId}/customize` | per-order line customization; total recalculated |
| GET/POST | `/api/inventory`, `/api/inventory/receive` | stock list (filterable by location) + intake |
| POST | `/api/inventory/reserve`, `/api/inventory/orders/{id}/reserve` | atomic ATP reservation (single or whole order, all-or-nothing) |
| POST | `/api/inventory/reservations/{id}/commit` \| `release` | ship / give back |
| GET/POST | `/api/inventory/transfers` + `dispatch` / `complete` / `cancel` | warehouse-to-warehouse with stock effects |
| GET/POST | `/api/production-orders` + `start` / `complete` / `cancel` | make-to-stock |
| GET/POST | `/api/rma` + `approve` / `reject` / `complete` | RMA management; completion applies restock + refund |
| GET | `/api/fulfillment-operations` (+`/couriers`) | deliveries; courier directory for assignment |
| POST | `/api/fulfillment-operations/{id}/assign-courier` | validates the assignee is a courier of the same company |
| GET/PUT | `…/my-deliveries`, `…/{id}/courier-status` | courier's own queue; ordered step advancement |
| GET | `/api/activity?orderId=…` | audit timeline; owner-scoped |

## Conventions

- **Pagination** everywhere on lists: `?page=&pageSize=` → `{ items, totalCount, … }`.
- **Status codes**: `422` = business rule (with `errorCode`), `404` = not found *or not yours*, `401/403` = auth, `429` = rate-limited.
- **Enums** travel as numbers in write requests and as names in read responses.
