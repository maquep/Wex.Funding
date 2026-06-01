# WEX Funding Platform

## How to Run It

**Prerequisites:** Docker, .NET 8 SDK

```bash
# 1. Start Postgres
docker compose up -d
dotnet tool install --global dotnet-ef

# 2. Apply the schema migration
dotnet ef database update \
  --project src/Wex.Funding.Infrastructure \
  --startup-project src/Wex.Funding.Api

# 3. Start the API
dotnet run --project src/Wex.Funding.Api
```

Swagger UI is available at **http://localhost:5244/swagger** (development mode only).

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
- Aggregates
- Value objects (Money, CurrencyCode, CardId)
- Domain events

## Wex.Funding.Infrastructure
- EF Core repos
- Treasury HTTP
client (Polly)

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
I used Clean Architecture with four-project layout for this problem. There is a strict boundary between Application and Infrastructure where the Application layer defines *what* is needed (port interfaces), the Infrastructure layer provides *how* (concrete adapters). This means the use cases are testable without a database or HTTP connection, as shown in the application-layer unit tests.

### Card and Transaction as separate aggregates
I treated Card and Transaction as separate aggregates rather than storing transactions inside the Card aggregate.
The main reason is scale. A card can have thousands of transactions over time, and loading the entire transaction history every time a new transaction is recorded would become inefficient very quickly.

The balance calculation (limit - transactions) is not an aggregate consistency rule that requires all transactions to be loaded into memory. Instead, the Application layer calculates the balance when needed using the card details and transaction data.
This keeps aggregate boundaries small, improves performance, and aligns with DDD principles where aggregates are designed around transactional consistency rather than real-world relationships.

### No MediatR / CQRS / Event Sourcing
I intentionally kept the solution simple and avoided introducing MediatR, CQRS, or Event Sourcing.

For a small API with only a few endpoints, these patterns would add additional complexity without providing much value.
The reviewer should see a system shaped to its actual problem, not a generic template applied regardless of fit.

### Result pattern for expected business outcomes
Expected business outcomes are handled using a `Result<TSuccess, TError>` pattern rather than exceptions.

Examples include:
- Card not found
- Transaction not found
- No exchange rate available

These are valid business outcomes rather than exceptional failures.
Exceptions are reserved for unexpected situations such as infrastructure failures, programming errors, or broken invariants.
Using a Result type makes all possible outcomes explicit and forces callers to handle both success and failure paths. Endpoints can then map specific error types directly to the correct HTTP response codes without relying on exception handling or string matching.

### Strongly-typed IDs
I used strongly typed IDs such as `CardId` and `TransactionId` instead of passing raw GUIDs throughout the application.
This provides compile-time safety and prevents accidentally using the wrong identifier type in repositories, use cases, or service calls. This is a simple pattern that improves maintainability and eliminates an entire class of bugs with no runtime overhead.

### `DateOnly` for transaction dates
Transaction dates represent calendar dates rather than specific points in time.
Using `DateTime` introduces unnecessary timezone considerations and ambiguity. `DateOnly` makes the intent explicit and avoids issues around UTC versus local time.
It also maps cleanly to PostgreSQL date columns.

### Decimal precision for monetary amounts
All monetary amounts use `decimal` in C# and `numeric(19,4)` in PostgreSQL.
Floating-point types are never used for money — they introduce precision errors that can be problematic in a payments system. The `Money` value object enforces this at the type level.

### EF Core `ComplexProperty` for Money
`Money` is mapped using EF Core 8's `ComplexProperty` rather than the older `OwnsOne` (owned entity) pattern. `ComplexProperty` maps value object properties directly as columns on the owning table without creating a shadow entity, requiring a discriminator column, or generating a separate `JOIN`. It is the semantically correct choice for an inline value object with no independent identity.

### Treasury API resilience (Polly)
The Treasury API is an external dependency, so resilience policies have been applied to the HTTP client using Polly.The configuration includes:
- Request timeout
- Retry with exponential backoff
- Circuit breaker protection
These policies help handle transient failures while preventing a downstream outage from impacting the entire application.

### Validation and error handling
- FluentValidation at the API boundary for request validation.
- ProblemDetails for all error responses.
- A global exception handler maps domain exceptions and Result errors to appropriate HTTP status codes. No raw exception messages or stack traces leak to clients.

## Project Structure
- **Wex.Funding.Api**: ASP.NET Core composition root. Minimal APIs, FluentValidation, 
  Serilog structured logging, Swagger, health checks. No business logic.
- **Wex.Funding.Application**: use cases, port interfaces, Result<T, E> type, application DTOs.
- **Wex.Funding.Domain**: Card and Transaction aggregates, value objects 
  (Money, CurrencyCode, CardId, TransactionId), domain events, domain exceptions. 
  Zero external dependencies.
- **Wex.Funding.Infrastructure**: EF Core repository implementations, Treasury rates 
  HTTP client with Polly resilience policies, in-memory rate cache.
---

## Testing Strategy

### Domain tests
`Wex.Funding.Domain.Tests` tests domain invariants with no infrastructure or mocking:
- `CurrencyCode`: valid/invalid ISO codes, equality
- `Money`: arithmetic, currency-mismatch exception
- `Card.Create`: invariants (negative limit rejected, zero accepted, event raised)
- `Transaction.Record`: all invariants (zero/negative amount, empty description, future date, today succeeds, past date succeeds)

### Application tests
`Wex.Funding.Application.Tests` uses NSubstitute to stub `ITransactionRepository`, `ICardRepository`, and `ITreasuryRatesClient`. The stub's responses are the test parameters; use-case logic is exercised in isolation.

Additional `GetCardBalanceInCurrency` tests verify that a missing card returns `CardNotFoundError` and a missing rate returns `BalanceRateNotAvailableError` — confirming that two distinct failure modes produce distinct typed errors, not the same one.
This suite is the primary signal because it tests the exact business rule that is most likely to be implemented subtly incorrectly.

### Integration tests

`Wex.Funding.Api.Tests` uses `WebApplicationFactory<Program>` with:
- **Testcontainers** (`postgres:16-alpine`) for a real database.
- **WireMock.NET** to stub the Treasury API HTTP boundary.

Tests cover the full end-to-end flow: create card → record transaction → get in target currency, plus validation rejection, unknown card/transaction (404), and the health endpoint.

### What was deliberately not tested
- **Controller routing**: covered by integration tests; a dedicated routing test adds no signal.
- **EF Core mappings against a mocked DbContext**: the integration tests cover this against a real Postgres instance, which is the only faithful test.
- **Trivial property getters**: no tests assert that `card.Id == card.Id`.

---

## AI Usage

AI tooling was used deliberately and visibly throughout this exercise. I used it for: project scaffolding and DI wiring; generating EF Core configuration boilerplate; drafting the Treasury API client and DTOs; generating edge-case test scenarios for the 6-month boundary logic, which I then reviewed and pruned.

Architecture decisions, aggregate boundary choices, the Result-vs-exception distinction, and the decision to exclude MediatR/CQRS/Event Sourcing were deliberate engineering judgement calls. I treated the AI as a junior pair programmer, directing the work and reviewing every output. The AI accelerated the parts of the build where speed-of-typing was the bottleneck (boilerplate, scaffolding, test enumeration); the parts that required judgement, what to model, where to draw boundaries, what not to include — were mine. This is how I use AI in production work too.

---

## Future Work / What I would Add at Production Scale
- **Idempotency keys** on `POST` endpoints (`Idempotency-Key` header) with dedup persistence, this is critical for payments to prevent double-charges on network retry.
- **OpenTelemetry exporters**: `ActivitySource` stubs are in place; production would wire them to Datadog/New Relic for tracing and metrics.
- **Authentication and authorisation**: API keys or OAuth2 client credentials. Current API is open.
- **Multi-currency cards**: current model assumes one credit-limit currency per card. A real product likely allows spending in multiple currencies.
- **Comprehensive currency mapping**: the Treasury API ISO-to-descriptive-name map covers 18 currencies. Production would derive this from the API's own reference data or a maintained external table.
- **Distributed rate cache**: replace in-memory `IMemoryCache` with Redis for multi-instance deployments.

---

## Trade-offs and Limitations
- **Currency mapping**: the ISO 4217 → Treasury descriptive name translation is a small in-memory dictionary covering 18 currencies. Requests for unmapped currencies return "no rate available." A production implementation would either maintain a comprehensive mapping or use the Treasury API's own discovery endpoint.
- **Rate cache**: in-memory only. Multiple API instances will independently populate their caches. Acceptable for a single-instance deployment; not for horizontal scale.
- **No authentication**: the API is open. All endpoints are accessible without credentials.
- **No rate-limit handling**: the Treasury API imposes request limits. The Polly retry policy handles transient errors but does not implement adaptive back-off for rate-limit (HTTP 429) responses.
- **`auto-migrate` in development**: `Database.MigrateAsync()` runs on startup in the Development environment. This is acceptable for local development convenience; a production deployment would use a dedicated migration step in the CI/CD pipeline.
