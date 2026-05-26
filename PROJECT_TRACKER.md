# ParcelTrack — Project Tracker

> Last updated: 2026-05-26
> Active branch: `develop`

---

## What this project is building

A **multi-tenant SaaS parcel tracking backend** — the infrastructure layer that powers shipment visibility for logistics companies or e-commerce platforms.

**End-to-end flow:**
```
Client (tenant) → Gateway → ShipmentService API
                                  ↓ (Outbox → Kafka)
                          ┌───────┼───────────────┐
                  TrackingService  NotificationService  WebhookDispatchService
                  (event log)      (email to buyer)     (webhooks to tenant)
```

---

## Overall Progress

| Service / Layer | Status |
|---|---|
| ShipmentService (Domain + Application + Infrastructure + API) | ✅ Complete |
| NotificationService (Application + Worker) | ✅ Complete |
| TrackingService (Domain + Application + Infrastructure + Worker) | ✅ Complete |
| WebhookDispatchService (Domain + Infrastructure + Worker) | ✅ Complete |
| Gateway (YARP + rate limiting + Serilog + OTel) | ✅ Complete |
| Shared.Contracts (Kafka topics + events) | ✅ Complete |
| Shared.Common (ApiErrorResponse + PagedResult<T>) | ✅ Complete |

---

## Cross-Cutting Concerns

| Concern | Status |
|---|---|
| Serilog structured logging — all services | ✅ Done |
| OpenTelemetry tracing — all services | ✅ Done |
| Health checks — all services | ✅ Done (`/health` on API + Gateway, DB checks on workers) |
| Keycloak JWT auth — ShipmentService | ✅ Active (`[Authorize]`, `ITenantContext`, config in appsettings.json) |
| Rate limiting — Gateway | ✅ Done (fixed window 100 req/min, 429 on breach) |

---

## Tests

| Project | Status |
|---|---|
| ShipmentService.UnitTests | ✅ 69/69 passing (domain + all 5 handlers) |
| ShipmentService.IntegrationTests | ✅ Exists (Testcontainers, real DB — not run in CI yet) |
| NotificationService.UnitTests | ❌ Stub only |
| TrackingService.UnitTests | ❌ Stub only |
| WebhookDispatchService | ❌ No test project |

---

## Remaining Work (ordered)

- [ ] **EF Core migrations** — ShipmentService and WebhookDispatchService (never run, DB won't start without these)
- [ ] **Unit tests** — NotificationService, TrackingService, WebhookDispatchService
- [ ] **Dockerfiles + docker-compose** — containerise all services
- [ ] **GitHub Actions CI** — build + test pipeline
