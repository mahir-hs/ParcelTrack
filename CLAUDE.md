# ParcelTrack — Claude Code Context

## Project Overview

**ParcelTrack** is a .NET 10 microservices platform for parcel shipment tracking. It follows Clean Architecture with CQRS (no MediatR — handlers are injected directly as Scoped services), Domain-Driven Design, and an Outbox pattern for reliable event publishing to Kafka.

## Solution Structure

```
src/
  Gateway/
    ParcelTrack.Gateway/                    # YARP API Gateway
  Services/
    ShipmentService/                        # Most active service
      ParcelTrack.ShipmentService.API/      # Controllers, middleware, DI extensions, Program.cs
      ParcelTrack.ShipmentService.Application/  # Commands, queries, handlers, DTOs, interfaces
      ParcelTrack.ShipmentService.Domain/   # Entities, enums, domain exceptions
      ParcelTrack.ShipmentService.Infrastructure/  # EF Core, Kafka, Outbox, repositories
    NotificationService/
      ParcelTrack.NotificationService.Application/
      ParcelTrack.NotificationService.Worker/
    TrackingService/
      ParcelTrack.TrackingService.Domain/
      ParcelTrack.TrackingService.Application/
      ParcelTrack.TrackingService.Infrastructure/
      ParcelTrack.TrackingService.Worker/
    WebhookDispatchService/
      ParcelTrack.WebhookDispatchService.Worker/
  Shared/
    ParcelTrack.Shared.Common/
    ParcelTrack.Shared.Contracts/           # Shared event contracts (cross-service)
tests/
  ShipmentService/
    ParcelTrack.ShipmentService.UnitTests/
    ParcelTrack.ShipmentService.IntegrationTests/  # Hits real DB — never mock DB here
  NotificationService/
    ParcelTrack.NotificationService.UnitTests/
  TrackingService/
    ParcelTrack.TrackingService.UnitTests/
```

## Architecture Patterns

- **Clean Architecture**: Domain → Application → Infrastructure → API. Dependencies only point inward.
- **CQRS (no MediatR)**: Commands and Queries in `Application/Commands/` and `Application/Queries/`. Handlers in `Application/Handler/`, registered as Scoped in `DependencyInjection.cs`, injected directly into controllers.
- **Outbox Pattern**: Handlers call `IEventProducer` → `OutboxEventProducer` writes to `OutboxMessages` table. `OutboxProcessor` background service reads and publishes to Kafka. Kafka `IKafkaProducer` is commented out locally (disabled until Kafka profile is active).
- **Multi-tenancy**: `ITenantContext` resolves `TenantId` and `UserId` from JWT claims. Global query filter on `Shipment` scopes all EF queries to the current tenant (currently commented out during early dev — `Guid.NewGuid()` is used as placeholder).
- **DI conventions**: Each layer has its own `DependencyInjection.cs` with an extension method. `Program.cs` is clean — calls `AddApplication()`, `AddInfrastructure()`, `AddApiServices()`, then `UseApiPipelineAsync()`.

## Tech Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 10 |
| Web framework | ASP.NET Core (controller-based) |
| ORM | EF Core 10 + Npgsql + `UseSnakeCaseNamingConvention()` |
| Database | PostgreSQL — native Windows install (NOT Docker) |
| Messaging | Confluent.Kafka 2.13 (KRaft, no Zookeeper) |
| Cache | Redis 7 (Alpine, Docker) |
| Auth | Keycloak 24 (JWT Bearer, `parceltrack` realm, commented out during early dev) |
| Mapping | Mapster (`MappingConfig.Configure()` called on startup) |
| API docs | Scalar (`/scalar` endpoint) |
| Entity config | `IEntityTypeConfiguration<T>`, applied via `ApplyConfigurationsFromAssembly` |

## Local Infrastructure (Docker Compose)

PostgreSQL runs **natively on Windows**. Redis, Kafka, and Keycloak run in Docker.

```powershell
# Messaging profile — Redis + Kafka
docker-compose --profile messaging up -d

# Full stack — adds Keycloak
docker-compose --profile messaging --profile auth up -d
```

**Required PostgreSQL databases** (create manually in DBeaver or psql):
```sql
CREATE DATABASE parceltrack_shipment;
CREATE DATABASE parceltrack_notification;
CREATE DATABASE parceltrack_tracking;
CREATE DATABASE parceltrack_webhook;
CREATE DATABASE parceltrack_keycloak;
```

**Required `.env` file** at repo root (not committed):
```
REDIS_PASSWORD=
POSTGRES_USER=
POSTGRES_PASSWORD=
KEYCLOAK_ADMIN_USER=
KEYCLOAK_ADMIN_PASSWORD=
KAFKA_CLUSTER_ID=
```

## Running the ShipmentService API

```powershell
dotnet run --project src/Services/ShipmentService/ParcelTrack.ShipmentService.API
```

- HTTP:  `http://localhost:5068`
- HTTPS: `https://localhost:7177`
- Scalar UI: `http://localhost:5068/scalar`

Connection string key: `ConnectionStrings:ShipmentDb` (set in `appsettings.Development.json`, not committed).

## EF Core Migrations (ShipmentService)

```powershell
dotnet ef migrations add <Name> `
  --project src/Services/ShipmentService/ParcelTrack.ShipmentService.Infrastructure `
  --startup-project src/Services/ShipmentService/ParcelTrack.ShipmentService.API

dotnet ef database update `
  --project src/Services/ShipmentService/ParcelTrack.ShipmentService.Infrastructure `
  --startup-project src/Services/ShipmentService/ParcelTrack.ShipmentService.API
```

Migrations assembly is the Infrastructure project; `ShipmentDbContextFactory` provides design-time context.

## Running Tests

```powershell
# Unit tests
dotnet test tests/ShipmentService/ParcelTrack.ShipmentService.UnitTests
dotnet test tests/NotificationService/ParcelTrack.NotificationService.UnitTests
dotnet test tests/TrackingService/ParcelTrack.TrackingService.UnitTests

# Integration tests — requires running PostgreSQL with parceltrack_shipment
dotnet test tests/ShipmentService/ParcelTrack.ShipmentService.IntegrationTests
```

**Never mock the database in integration tests.** They must hit a real PostgreSQL instance.

## Domain Rules (ShipmentService)

- `Shipment` is created via `Shipment.Create(...)` factory — never via constructor.
- Status transitions are enforced by `AllowedTransitions` state machine in the entity. Never set `Status` directly.
- `MaxDeliveryAttempts = 3` — `OutForDelivery` transitions increment `DeliveryAttempts`; exceeding the max throws `MaxDeliveryAttemptsExceededException`.
- Terminal states: `Delivered`, `Cancelled` — no further transitions allowed.
- Domain exceptions all inherit `DomainException` (not `Exception`).
- `BuyerEmail` is nullable — B2B tenants may omit it.
- `TenantId` and `UserId` always come from JWT (`ITenantContext`) — never from request body.

## Key Conventions

- Handlers are plain Scoped classes — no `IRequestHandler<,>` interface, no pipeline behaviors.
- EF column names use snake_case (`UseSnakeCaseNamingConvention`).
- Entity configs live in `Infrastructure/Persistence/Configurations/` as `IEntityTypeConfiguration<T>`.
- Each new service follows the same 4-layer structure (Domain / Application / Infrastructure / API or Worker).
- `Authorize` attribute and `ITenantContext` usage are commented out during early dev; `Guid.NewGuid()` is the placeholder. Re-enable when Keycloak integration is active.
- Commit style: Conventional Commits — `feat:`, `fix:`, `chore:`, `refactor:`, `update:`.
- Never commit secrets — use `appsettings.Development.json` (gitignored) or `.env`.

## Active Work

Branch: `feature/shipment-service-api` — ShipmentService API layer is the active development surface.
