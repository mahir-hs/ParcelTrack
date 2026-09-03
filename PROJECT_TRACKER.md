# ParcelTrack — Project Tracker

> Last updated: 2026-09-03
> Active branch: `develop` (23 commits ahead of `main`)

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
| WebhookDispatchService (Worker + subscription API) | ✅ Complete |
| Gateway (YARP + rate limiting + Serilog + OTel) | ✅ Complete |
| Shared.Contracts (Kafka topics + events) | ✅ Complete |
| Shared.Common (ApiErrorResponse + PagedResult<T>) | ✅ Complete |

### Public surface

| Route | Service |
|---|---|
| `POST/GET/PUT/DELETE /v1/shipments…` (create, get, paged list, status update, cancel, events) | ShipmentService |
| `GET /v1/track/{trackingNumber}` (public, anonymous) | ShipmentService |
| `GET/POST/DELETE /v1/webhooks` | WebhookDispatchService |
| `/health` | Gateway, ShipmentService, all workers |

All three route groups are proxied through the YARP gateway (`shipment-cluster`, `webhook-cluster`); rate limiting = fixed window 100 req/min.

---

## Cross-Cutting Concerns

| Concern | Status |
|---|---|
| Serilog structured logging — all services | ✅ Done |
| OpenTelemetry tracing — all services | ✅ Done (console exporter only — no collector/backend yet) |
| Health checks — all services | ✅ Done |
| Keycloak JWT auth | ✅ Active on ShipmentService + WebhookDispatchService (`[Authorize]`, `tenantId` claim) |
| Multi-tenant query filter | ✅ Active (`HasQueryFilter` on `Shipment`) |
| Rate limiting — Gateway | ✅ Done |
| Kafka end-to-end (Outbox → producer → 3 consumers) | ✅ Done |
| Redis idempotency (`X-Idempotency-Key` on shipment create) | ✅ Done |
| Outbox dead-lettering (`MaxAttempts = 5`) | ✅ Done |
| Webhook HMAC signing + retry | ✅ Done |
| EF Core migrations | ✅ Shipment, Tracking, Webhook all have initial migrations |
| Dockerfiles + compose `app` profile | ✅ Done (5 images) |

---

## Tests

| Project | Tests | Status |
|---|---|---|
| ShipmentService.UnitTests | 69 | ✅ Domain + all 5 handlers |
| ShipmentService.IntegrationTests | 8 | ✅ Testcontainers + real Postgres, not in CI |
| NotificationService.UnitTests | 17 | ✅ Both handlers |
| TrackingService.UnitTests | 17 | ✅ Domain + handlers |
| WebhookDispatchService.UnitTests | 35 | ✅ Domain + dispatch handler |

**Verified 2026-09-03: solution builds clean (0 errors) on SDK 10.0.400; all 138 unit tests pass.** Integration tests not run (need Docker for Testcontainers).

---

## Blockers

- 🟡 **No `.env` at repo root** — `docker-compose` needs `REDIS_PASSWORD`, `POSTGRES_USER/PASSWORD`, `KEYCLOAK_ADMIN_*`, `KAFKA_CLUSTER_ID`. Infra profiles won't come up cleanly without it.
- 🟡 **Vulnerable packages** — `Microsoft.OpenApi` 2.0.0 (GHSA-v5pm-xwqc-g5wc, high) in ShipmentService.API + IntegrationTests; `SSH.NET` 2025.1.0 (GHSA-q939-rpr3-3284, high, transitive via Testcontainers). Bump both.

---

## Remaining Work (ordered)

- [ ] **GitHub Actions CI** — `.github/workflows/` is empty on `develop`; the workflow lives unmerged on `feat/ci-pipeline` (commit `49aec99`). Merge it.
- [ ] **Merge `develop` → `main`** — `main` is 23 commits behind and no longer represents the project.
- [ ] **README.md is empty** (0 lines) — needs setup, architecture diagram, run instructions.
- [ ] **Secrets hygiene** — `appsettings*.json` are tracked in git with `Password=admin` / `Password=postgres`. Move to env vars or user-secrets; add a committed `.env.example`.
- [ ] **Re-enable ShipmentService startup migration** or document the manual step (currently commented out in `WebApplicationExtensions`, commit `2e78e11`). Tracking/Webhook workers still auto-migrate — inconsistent.
- [ ] **Integration tests in CI** — Testcontainers suite is never run automatically.
- [ ] **Prune stale branches** — 12 remote feature branches, most fully merged into `develop`.
- [ ] **Package warnings** — 2 × NU1903 (above) and 2 × NU1510 (redundant `Microsoft.Extensions.Hosting` / `Microsoft.Extensions.Http` refs in WebhookDispatchService).
- [ ] **OTel exporter** — console only; wire OTLP → Jaeger/Tempo for real tracing.
- [ ] **NotificationService has no persistence/migration** even though `parceltrack_notification` DB is documented — confirm intentional (currently log/SMTP only, no notification history).
