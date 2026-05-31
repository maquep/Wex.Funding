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

