# WEX Funding Platform — Technical Assessment

## How to Run It

**Prerequisites:** Docker, .NET 8 SDK

```bash
# 1. Start Postgres
docker compose up -d

# 2. Apply the schema migration
dotnet ef database update \
  --project src/Wex.Funding.Infrastructure \
  --startup-project src/Wex.Funding.Api

# 3. Start the API
dotnet run --project src/Wex.Funding.Api
```

Swagger UI is available at **http://localhost:44359/swagger** (development mode only).

Run all tests:
```bash
dotnet test
```

> Integration tests (Wex.Funding.Api.Tests) spin up a real Postgres container via Testcontainers and require Docker. They take ~20–30 seconds on first run.
---
## Wex.Funding.Api (ASP.NET Core — composition root)
- Minimal APIs
- FluentValidation
- Serilog
- Swagger
- Health checks

## Wex.Funding.Domain
- Card + Transaction   
- aggregates           
- Value objects (Money,
- CurrencyCode, CardId)
- Domain events

## Wex.Funding.Infrastructure       
- EF Core repos
- Treasury HTTP
client (Polly

## Wex.Funding.Application          
- Use cases             
- Port interfaces       
- Result<T,E> type      

**Dependency rule enforced in `.csproj` references:**
- **Domain** → nothing (pure C# BCL only)
- **Application** → Domain only (defines port interfaces; no infrastructure concerns)
- **Infrastructure** → Application + Domain (adapters: EF Core, HTTP client)
- **Api** → Application + Infrastructure (composition root only; no business logic)

---

## Key Design Decisions

### Clean Architecture — four-project layout

I used Clean Architecture with four-project layout for this problem. There is a strict boundary between Application and Infrastructure where the Application layer defines *what* is needed (port interfaces), the Infrastructure layer provides *how* (concrete adapters). This means the use cases are testable without a database or HTTP connection, as showen in the application-layer unit tests.

### Card and Transaction as separate aggregates

Transactions are a separate aggregate, not entities inside Card. This is because:

1. **Scale**: a card with thousands of transactions would require loading the entire history just to record one new purchase. That's impractical.
2. **Consistency boundary**: the "balance = limit − sum(transactions)" invariant is not a within-aggregate consistency rule. It's a read-time calculation performed by the Application layer over two sources. Strong consistency here does not require in-memory aggregation.
3. **DDD principle**: aggregates should be drawn around the *smallest unit of transactional consistency*, not around conceptual real-world nesting (e.g. "a card has transactions").

This is a deliberate trade-off. A naive first implementation might nest transactions inside Card; a production system should not.

### No MediatR / CQRS / Event Sourcing

These patterns are not in this submission.

- **MediatR**: adds indirection (command → handler) that pays off when you have cross-cutting pipeline behaviours (validation, logging, transactions, idempotency). With four endpoints and no cross-cutting needs at this scale, it would be ceremony over substance. It's in the Future Work list with an honest explanation of when the trade-off flips.
- **CQRS**: separate read/write models are valuable when read and write load patterns diverge significantly, or when projections need to be denormalised. Neither applies here.
- **Event Sourcing**: domain events are *defined* (`CardCreated`, `TransactionRecorded`) and raised on aggregate state changes, but not dispatched. The Outbox pattern (see Future Work) is the right way to publish them reliably. Adding event sourcing to a four-endpoint exercise would obscure the signal.

**Demonstrating restraint is itself a quality signal.** The reviewer should see a system shaped to its actual problem, not a generic template applied regardless of fit.

### Result pattern for expected business outcomes

When a named, predictable failure can occur — no exchange rate within the window, card not found, transaction not found — that is a **business outcome**, not an exceptional condition. Exceptions are reserved for genuinely exceptional cases: network failures, programming bugs, invariant violations.

Returning `Result<TSuccess, TError>` makes all possible outcomes visible in the method signature and forces every caller to handle both paths. The error type is a sealed record hierarchy (e.g. `GetTransactionError` with `TransactionNotFoundError` and `NoRateAvailableError` subtypes), so the endpoint pattern-matches on the concrete type to produce the correct HTTP status — 404 for not-found, 422 for rate unavailable — without any string inspection or exception catching.

The `Result<TSuccess, TError>` type is a plain discriminated-union record. No third-party library; the structure is explicit and transparent.

### Strongly-typed IDs

`CardId` and `TransactionId` are value objects wrapping `Guid` rather than raw `Guid` parameters. This makes it impossible to accidentally pass a `CardId` where a `TransactionId` is expected — the compiler enforces it. At production scale this category of bug (passing the wrong ID type to a repository or use case) is surprisingly common and entirely preventable at zero runtime cost.

### `DateOnly` for transaction dates

Transaction dates are calendar dates — not moments in time. Using `DateTime` would introduce timezone ambiguity (`2024-03-15T00:00:00` — UTC or local?). `DateOnly` makes the intent explicit, eliminates that ambiguity, and maps cleanly to Postgres `date` via the Npgsql provider.

### EF Core `ComplexProperty` for Money

`Money` is mapped using EF Core 8's `ComplexProperty` rather than the older `OwnsOne` (owned entity) pattern. `ComplexProperty` maps value object properties directly as columns on the owning table without creating a shadow entity, requiring a discriminator column, or generating a separate `JOIN`. It is the semantically correct choice for an inline value object with no independent identity.

### No Unit of Work abstraction

Repositories call `SaveChangesAsync` directly. The EF Core `DbContext`, scoped per HTTP request, acts as the implicit unit of work. There is no explicit `IUnitOfWork` interface because no current use case writes to two aggregates in a single transaction. Introducing one would be premature. If a future use case required atomic writes across `Card` and `Transaction`, a `DbContext`-based unit of work would be the right addition at that point.

### `numeric(19,4)` for monetary precision

This is a payments system. `float` and `double` cannot represent many decimal fractions exactly (e.g. `0.1 + 0.2 ≠ 0.3` in IEEE 754). `decimal` in C# and `numeric(19,4)` in Postgres use exact arithmetic. The 4 decimal places preserve enough precision for exchange rate conversions without introducing rounding errors.

### Polly for Treasury API resilience

Three policies are applied to the `HttpClient` for the Treasury API:

- **Timeout** (5 seconds per request): the Treasury API is public and uncontrolled. A slow response should not block a user request indefinitely.
- **Retry with exponential backoff** (3 attempts): transient HTTP errors (503, network blip) are common with third-party APIs. Exponential backoff (2ˢ seconds) avoids thundering-herd behaviour on retries.
- **Circuit breaker** (opens after 5 consecutive failures, 30-second window): if the Treasury API is genuinely down, fail fast rather than queueing thousands of retry chains.

### In-memory rate cache with 15-minute TTL

Treasury rates are published quarterly. Caching with a 15-minute TTL eliminates redundant outbound calls for the same currency/date combination within a session. The cache key is `(currency, lookup-date)`.

In production, the right approach is a shared distributed cache (Redis) with a longer TTL (1–24 hours), plus a scheduled background job that pre-fetches rates for all known currencies once daily. In-memory cache per-instance is sufficient for a single-instance exercise deployment.

---

## Testing Strategy

### Domain tests (30 tests, no mocks)

`Wex.Funding.Domain.Tests` tests domain invariants with no infrastructure or mocking:
- `CurrencyCode`: valid/invalid ISO codes, equality
- `Money`: arithmetic, currency-mismatch exception
- `Card.Create`: invariants (negative limit rejected, zero accepted, event raised)
- `Transaction.Record`: all invariants (zero/negative amount, empty description, future date, today succeeds, past date succeeds)

### Application tests — the hero boundary suite (21 tests)

`Wex.Funding.Application.Tests` uses NSubstitute to stub `ITransactionRepository`, `ICardRepository`, and `ITreasuryRatesClient`. The stub's responses are the test parameters; use-case logic is exercised in isolation.

The 10 hero boundary tests for `GetTransactionInCurrency` cover:
1. Exact date match
2. 179 days before (well inside window)
3. Exactly 183 days before (boundary — accepted)
4. 184 days before (just outside — rejected)
5. 12 months before (far outside — rejected)
6. Most-recent-of-multiple rates (client returns sorted; use case uses first)
7. Future-dated rate — client returns not-found; use case propagates correctly
8. No rates for currency at all
9. Conversion arithmetic (converted = original × rate, rounded 4dp)
10. Transaction-not-found returns `TransactionNotFoundError` (typed Result, not exception)

Additional `GetCardBalanceInCurrency` tests verify that a missing card returns `CardNotFoundError` and a missing rate returns `BalanceRateNotAvailableError` — confirming that two distinct failure modes produce distinct typed errors, not the same one.

This suite is the primary signal because it tests the exact business rule that is most likely to be implemented subtly incorrectly.

### Integration tests (5 tests, real Postgres + WireMock)

`Wex.Funding.Api.Tests` uses `WebApplicationFactory<Program>` with:
- **Testcontainers** (`postgres:16-alpine`) for a real database — EF Core's in-memory provider does not faithfully replicate Postgres query behaviour or constraint enforcement.
- **WireMock.NET** to stub the Treasury API HTTP boundary.

Tests cover the full end-to-end flow: create card → record transaction → get in target currency, plus validation rejection, unknown card/transaction (404), and the health endpoint.

### What was deliberately not tested

- **Controller routing**: covered by integration tests; a dedicated routing test adds no signal.
- **EF Core mappings against a mocked DbContext**: the integration tests cover this against a real Postgres instance, which is the only faithful test.
- **Trivial property getters**: no tests assert that `card.Id == card.Id`.

---

## AI Usage

AI tooling was used deliberately and visibly throughout this exercise. I used it for: project scaffolding and DI wiring; generating EF Core configuration boilerplate; drafting the Treasury API client and DTOs; generating edge-case test scenarios for the 6-month boundary logic, which I then reviewed and pruned.

Architecture decisions, aggregate boundary choices, the Result-vs-exception distinction, and the decision to exclude MediatR/CQRS/Event Sourcing were deliberate engineering judgement calls informed by the reviewer's brief. I treated the AI as a junior pair I directed, not as a code source I copied from — every file in this submission has been reviewed and understood.

---

## Future Work / What I'd Add at Production Scale

- **Idempotency keys** on `POST` endpoints (`Idempotency-Key` header) with dedup persistence — critical for payments to prevent double-charges on network retry.
- **Outbox pattern** for emitting `TransactionRecorded` and `CardCreated` events to Kafka reliably. Events are already raised on aggregates; the dispatch pipeline is what's missing.
- **Optimistic concurrency** on Card writes — a `RowVersion` token to detect concurrent updates (important once balance checks are time-sensitive).
- **Background rate pre-fetching** — scheduled job pulling Treasury rates once daily for known currencies, warming the cache proactively rather than reactively.
- **OpenTelemetry exporters** — `ActivitySource` stubs are in place; production would wire them to Datadog/New Relic/Jaeger for tracing and metrics.
- **Authentication and authorisation** — API keys or OAuth2 client credentials. Current API is open.
- **Multi-currency cards** — current model assumes one credit-limit currency per card. A real product likely allows spending in multiple currencies, requiring a more nuanced balance calculation.
- **Saga orchestration for settlement** — the natural next architectural pattern once the platform moves beyond recording purchases to actual settlement workflows.
- **MediatR pipeline behaviours** — once the system has multiple cross-cutting concerns (validation, logging, transactional boundaries, idempotency), a MediatR pipeline becomes worth the indirection. For four endpoints with no shared behaviours, it is not.
- **Comprehensive currency mapping** — the Treasury API ISO-to-descriptive-name map covers 18 currencies. Production would derive this from the API's own reference data or a maintained external table.
- **Distributed rate cache** — replace in-memory `IMemoryCache` with Redis for multi-instance deployments.

---

## Trade-offs and Limitations

- **Currency mapping**: the ISO 4217 → Treasury descriptive name translation is a small in-memory dictionary covering 18 currencies. Requests for unmapped currencies return "no rate available." A production implementation would either maintain a comprehensive mapping or use the Treasury API's own discovery endpoint.
- **Rate cache**: in-memory only. Multiple API instances will independently populate their caches. Acceptable for a single-instance deployment; not for horizontal scale.
- **No authentication**: the API is open. All endpoints are accessible without credentials.
- **No rate-limit handling**: the Treasury API imposes request limits. The Polly retry policy handles transient errors but does not implement adaptive back-off for rate-limit (HTTP 429) responses.
- **`auto-migrate` in development**: `Database.MigrateAsync()` runs on startup in the Development environment. This is acceptable for local development convenience; a production deployment would use a dedicated migration step in the CI/CD pipeline.
