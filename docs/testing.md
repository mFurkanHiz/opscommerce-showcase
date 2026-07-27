# Testing

## The strategy in one paragraph

Business rules live in a dependency-free domain layer, so the most valuable tests are plain unit tests that run in milliseconds with no mocks and no database. Service-layer behavior that *can* run against EF Core's in-memory provider is tested that way (tenant scoping, validation rules, audit writes). The paths that deliberately use raw conditional SQL for concurrency **cannot** be unit-tested honestly — so they are verified against the live environment with real HTTP calls instead of being faked.

## What runs where

| Suite | Count | Runs | Covers |
|---|---|---|---|
| Domain tests (in this repo) | **31** | `dotnet test`, CI on every push | state machines, ATP arithmetic, deposit/partial payment, RMA rules, per-order customization |
| Service tests (private repo) | 50+ | CI gate before every deploy | tenant isolation, ownership checks, server-authoritative pricing, RMA limits, audit-log writes, masked payment methods |
| Live verification | scripted | after every deploy | the concurrency paths + full end-to-end flows |

## Why some things are *not* unit-tested

The stock reservation and the payment confirmation use single conditional `UPDATE` statements (see [code highlights](../code-highlights/atomic-reservation.md)). EF's in-memory provider does not execute raw update semantics, and mocking them would test the mock, not the behavior. Pretending otherwise would be false confidence.

Instead, these paths are exercised against the deployed environment after each release:

- reserve → ATP drops → commit → on-hand drops (full lifecycle),
- a transfer moving real quantities between two warehouses,
- deposit → `PartiallyPaid` → balance → `Paid`,
- a failed payment leaving the order retryable (`PaymentFailed`), then a successful retry,
- a TTL reservation being auto-released by the background worker (observed live: reserved 1 → 0 after expiry),
- cross-tenant access attempts returning 404/422.

A test suite that states its blind spots and covers them another way is worth more than a dashboard of green mocks.

## Test style

- Given/when/then in plain code; one behavior per test.
- Assertions on **stable error codes**, not message text — messages may change, contracts may not.
- No time bombs: expiry logic takes the clock as a parameter (`IsExpired(nowUtc)`), so time-based rules are tested deterministically.

## CI as a gate, not a report

The pipeline order is *build → test → deploy*. A red test does not produce a deployable artifact — the deployment job never runs. The same build+test gate runs in this repository on every push (see the badge in the README).
