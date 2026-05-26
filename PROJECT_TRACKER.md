# ParcelTrack — Project Tracker

> Last updated: 2026-05-26 (Day 1 complete)
> Active branch: `feature/shipment-service-api`

---

## What this project is building

A **multi-tenant SaaS parcel tracking backend** — the infrastructure layer that powers shipment visibility for logistics companies or e-commerce platforms. Think of it as a simplified version of what sits behind DHL or FedEx's tracking API.

**End-to-end flow:**
```
Client (tenant) → Gateway → ShipmentService API
                                  ↓ (Outbox)
                              Kafka topics
                          ↙         ↓         ↘
              TrackingService  NotificationService  WebhookDispatchService
              (event log)      (email/SMS to buyer) (webhooks to tenant)
```

---

## Overall Progress

| Service | Domain | Application | Infrastructure | API/Worker | Tests |
|---|---|---|---|---|---|
| ShipmentService | ✅ Done | ✅ Done | ✅ Done | ✅ Done | ❌ None |
| NotificationService | — | ✅ Done | — | ✅ Done | ❌ None |
| TrackingService | ❌ Empty | ❌ Empty | ❌ Empty | 🏗️ Scaffold only | ❌ None |
| WebhookDispatchService | ❌ Empty | ❌ Empty | ❌ Empty | 🏗️ Scaffold only | ❌ None |
| Gateway | — | — | — | 🏗️ Scaffold only | — |
| Shared.Contracts | ✅ Done | — | — | — | — |
| Shared.Common | ❌ Empty | — | — | — | — |

---

## ShipmentService — Detailed Status (reference implementation)

### ✅ Done

**Domain**
- `Shipment` aggregate with factory method `Shipment.Create(...)`
- Full status state machine (`AllowedTransitions` dictionary, `UpdateStatus`, `Cancel`)
- `ShipmentEvent` entity — immutable event history per shipment
- `ShipmentStatus` and `CarrierType` enums
- All domain exceptions: `DomainException` base, `ShipmentNotFoundException`, `InvalidShipmentStatusTransitionException`, `ShipmentAlreadyTerminatedException`, `MaxDeliveryAttemptsExceededException`, `DuplicateTrackingNumberException`
- Max delivery attempts enforcement (3 attempts)
- Terminal state guard (`IsTerminal`)

**Application**
- Commands: `CreateShipmentCommand`, `UpdateShipmentStatusCommand`, `CancelShipmentCommand`
- Queries: `GetShipmentByIdQuery`, `GetShipmentsQuery`
- Handlers: all 5 handlers implemented (Create, UpdateStatus, Cancel, GetById, GetPaged)
- `GetShipmentByTrackingNumberQueryHandler` — public tracking lookup
- Interfaces: `IShipmentRepository`, `IEventProducer`, `IUnitOfWork`, `ITenantContext`
- DTOs: `ShipmentDto`, `ShipmentEventDto`, `ShipmentSummaryDto`, `PagedResult<T>`, `PublicTrackingDto`
- Mapster config: `MappingConfig`, `ShipmentMappingExtensions`

**Infrastructure**
- `ShipmentDbContext` with `ApplyConfigurationsFromAssembly`
- EF Core entity configs: `ShipmentConfiguration`, `ShipmentEventConfiguration`, `OutboxMessageConfiguration`
- Snake_case naming convention (`UseSnakeCaseNamingConvention`)
- `ShipmentRepository` — full implementation (GetById, GetByTracking, GetPaged, Add, public bypass)
- `UnitOfWork` wrapping `SaveChangesAsync`
- `ShipmentDbContextFactory` for EF migrations design-time
- `OutboxMessage` entity + `OutboxEventProducer` (writes to DB, not Kafka directly)
- `OutboxProcessor` background service — `FOR UPDATE SKIP LOCKED`, batch of 20, 5s polling
- `KafkaProducer` — idempotent, Acks.All, retry-safe, headers with event-type
- DI split into `PersistenceExtensions`, `MessagingExtensions`, `BackgroundServiceExtensions`

**API**
- `ShipmentsController` — POST, GET (paged), GET by ID, PUT status, DELETE (cancel), GET events
- `TrackingController` — public `GET /v1/track/{trackingNumber}` (anonymous, no tenant filter)
- `ExceptionHandlingMiddleware` — maps all domain exceptions to correct HTTP status codes
- `AuthenticationExtensions` — Keycloak JWT Bearer wiring (ready, commented out)
- `OpenApiExtensions` — Scalar docs at `/scalar`
- `WebApplicationExtensions` — clean middleware pipeline via `UseApiPipelineAsync()`
- `TenantContext` — reads `tenantId` and `sub` claims from JWT
- Clean `Program.cs` — 3 lines of setup + run
- Auto-migration on startup in Development environment

**Shared.Contracts**
- `Topics` — central Kafka topic registry (`shipment.created`, `shipment.status.changed`, `notification.failed`, `webhook.failed`)
- `ShipmentCreatedEvent`, `ShipmentStatusChangedEvent`

---

### ⚠️ Incomplete / Needs Work

| Item | Issue |
|---|---|
| EF migrations | Not confirmed applied — need to run `dotnet ef migrations add Initial` |
| `OutboxMessage.RecordFailure` | No max retry limit or dead-letter logic |

### ✅ Resolved (Day 1)

| Item | Fix |
|---|---|
| `[Authorize]` on `ShipmentsController` | Enabled |
| `ITenantContext` placeholders | All `Guid.NewGuid()` replaced with real `_tenantContext` calls |
| Global tenant query filter | Enabled in `ShipmentDbContext` |
| `IKafkaProducer` DI registration | Enabled in `MessagingExtensions` |
| `Console.WriteLine` debug lines | Removed from `ShipmentRepository` and `UpdateShipmentStatusCommandHandler` |
| Keycloak port in appsettings | Fixed `8080` → `8180` |
| `MiniProfiler` incompatible package | Removed from Infrastructure csproj |

---

### ❌ Not Started (ShipmentService)

- [ ] Unit tests for domain (state machine transitions, factory, exceptions)
- [ ] Unit tests for application handlers (mock repository + event producer)
- [ ] Integration tests for all endpoints (real DB)
- [ ] Dead-letter handling for failed outbox messages (max retry + DLQ)

---

## Other Services — All Not Started

### NotificationService ✅ Core done (Day 1)

- [x] Application: `NotificationDto`, `INotificationSender`, `ShipmentCreatedHandler`, `ShipmentStatusChangedHandler`
- [x] Worker: real Kafka consumer (`shipment.created` + `shipment.status.changed`), manual commit, error backoff
- [x] `LogNotificationSender` — logs to stdout (swap for SMTP/SendGrid before prod)
- [x] `KafkaSettings` typed config, `appsettings.json` wired
- [ ] Real email sender (SMTP or SendGrid) — replace `LogNotificationSender`
- [ ] Tests

### TrackingService
- [ ] Domain: `TrackingRecord` entity, tracking snapshot model
- [ ] Application: Kafka consumer handler, query handlers
- [ ] Infrastructure: Kafka consumer, own PostgreSQL DB (`parceltrack_tracking`)
- [ ] Worker: replace scaffold with Kafka consumer loop
- [ ] Tests

### WebhookDispatchService
- [ ] Domain: `WebhookSubscription`, `WebhookDelivery` entities
- [ ] Application: dispatch command, retry logic
- [ ] Infrastructure: Kafka consumer for `shipment.status.changed`, HTTP client for outbound calls, `webhook.failed` producer
- [ ] Worker: replace scaffold with Kafka consumer loop
- [ ] Tests

### Gateway
- [ ] Configure YARP reverse proxy routes to ShipmentService (and others as they come up)
- [ ] JWT pass-through or gateway-level auth
- [ ] Rate limiting
- [ ] Tests

---

## Cross-Cutting Concerns — Not Started

- [ ] Structured logging (Serilog + Seq or similar)
- [ ] Health check endpoints (`/health/ready`, `/health/live`) for all services
- [ ] Distributed tracing (OpenTelemetry)
- [ ] `Shared.Common` — shared middleware, base classes, or utilities
- [ ] Docker Compose: re-enable Kafka (currently commented out)
- [ ] `appsettings.Development.json` templating / docs for new devs
- [ ] CI pipeline (GitHub Actions — build, test, lint)

---

## 4-Day AWS Deployment Plan

| Day | Focus | Status |
|---|---|---|
| 1 | Clean up ShipmentService, activate auth + Kafka, build NotificationService | ✅ Done |
| 2 | Containerize both services, deploy to AWS (ECS + RDS + MSK) | ⏳ Next |
| 3 | GitHub Actions CI/CD pipeline | ❌ |
| 4 | Health checks, structured logging, README with architecture diagram + live URL | ❌ |

## Remaining Backlog (post-deployment)

- [ ] EF migrations — run `dotnet ef migrations add Initial` for ShipmentService
- [ ] ShipmentService unit + integration tests
- [ ] Real email sender in NotificationService (SMTP or SendGrid)
- [ ] TrackingService — full implementation
- [ ] WebhookDispatchService — full implementation
- [ ] Gateway — YARP routing, rate limiting
- [ ] Dead-letter handling for Outbox failures
- [ ] Distributed tracing (OpenTelemetry)
