# ParcelTrack — Chief Architect's Project Plan
### Multi-Carrier Parcel Tracking & Notification Platform

---

## 1. Project Overview

**What it is:** A backend-heavy SaaS platform that unifies parcel tracking across multiple Bangladeshi couriers (Steadfast, Pathao, Redx) and proactively notifies end customers when their shipment status changes — via email and real-time WebSocket push.

**What it is NOT:** A frontend-heavy app. You are building backend infrastructure. A minimal UI (or just Swagger) is sufficient.

---

### 1.1 The Real Problems Being Solved

**B2C — The Facebook Seller Problem**

Riya sells clothes on Facebook. A customer orders a kurti, pays bKash, Riya ships via Steadfast. Now the buyer messages Riya every day: *"where is my parcel?"* Riya manually checks Steadfast's website, copies the status, pastes it in Messenger — for 50 orders a day.

**ParcelTrack solves this:**
- Riya registers the tracking number when she ships
- The buyer automatically gets notified when status changes: *"Your parcel is out for delivery"*
- Riya sees every parcel across every courier in one dashboard, instead of three courier sites

> **Honest limitation.** Couriers already SMS the recipient about their own leg of the journey, so a seller using a *single* courier gets much of this for free. The value here is thin until the seller uses more than one courier, or wants the notification to carry their own brand rather than the courier's. The durable value is in the B2B case below — treat B2C as an on-ramp, not the core market.

**B2B — The Multi-Courier Merchant Problem**

A mid-size online retailer — fashion, electronics, cosmetics — ships 500-2000 parcels daily and splits them across Pathao, Steadfast, and Redx depending on zone, cost, and who has capacity today. Three couriers means three API integrations to build, three sets of credentials to rotate, three status vocabularies to reconcile, and three dashboards to check. Each one breaks independently.

They want **one API**: register a shipment, receive a webhook when status changes, trigger their own customer notifications.

ParcelTrack becomes the middleware layer between their platform and every courier.

> **Targeting note.** The customer is a merchant who ships through *multiple third-party couriers*. Companies running their own delivery fleet (Chaldal, Daraz in-house) are explicitly **not** the target — they have no aggregation problem to solve. The pitch only lands where courier fragmentation is real.

**The model is B2B2C:** Businesses integrate your API (B2B), their end customers receive notifications (B2C). This mirrors the multi-tenant OTA platform you built at work — same architecture pattern, different domain.

---

### 1.2 How Does Your System Know the Courier Status?

There are two mechanisms — your system supports both:

**Option 1 — Courier Pushes (Webhooks) ✅ Primary**
The courier's system calls your API when status changes.
`Steadfast ships parcel → status changes → Steadfast hits POST /webhooks/steadfast → your system processes it`
Real-time, no wasted API calls. Steadfast and Pathao support this.

**Option 2 — You Poll the Courier ✅ Fallback**
Your Tracking Service calls the courier's API every 30 seconds and checks if status changed.
Used for couriers that don't support webhooks, or as a safety net.

The `ICarrierAdapter` interface abstracts which mechanism is used. Whether status arrives via webhook push or polling, the rest of the system doesn't care — it just receives a `CarrierTrackingResult` and processes it identically.

---

### 1.2a "Why Not Just Use the Courier's Own Tracking?"

The obvious objection, and the one to have an answer ready for. Couriers do notify recipients. What they cannot do:

| Gap | Why the courier can't close it |
|---|---|
| **One integration instead of N** | Steadfast has no reason to normalize Pathao's API. Somebody has to sit above all of them. |
| **Normalized status vocabulary** | Each courier names states differently. "Pending" means different things at different couriers. |
| **Cross-courier analytics** | *Which courier actually has the worst failed-delivery rate in Chittagong?* No single courier dashboard can answer this — it is the argument that survives every objection. |
| **Merchant-branded notifications** | The courier brands the message as the courier. Merchants want their own name on it. |
| **Exception detection** | Flagging the parcel stuck four days before the customer complains, across all couriers at once. |
| **Webhooks where none exist** | Redx has no webhook support at all. The aggregator polls and synthesizes the push. |

**Precedent:** AfterShip built a substantial business on exactly this shape — multi-carrier tracking APIs, branded tracking pages, unified webhooks — alongside EasyPost and Shippo in the shipping-API layer. The category is proven; the opportunity here is that it is underserved for Bangladeshi couriers specifically.

---

### 1.3 Real Carrier APIs (Bangladesh)

| Carrier | Auth Method | Webhook Support | Docs / Access |
|---|---|---|---|
| **Steadfast** | API Key + Secret Key | ✅ Yes | Register at steadfast.com.bd as merchant. Credentials in dashboard. Base URL: `https://portal.steadfast.com.bd/api/v1` |
| **Pathao** | OAuth2 (client_id + secret + username + password) | ✅ Yes | Register at merchant.pathao.com. Sandbox URL given after registration |
| **Redx** | API Key | ❌ Polling only | developer access at redx.com.bd/developer-api |

**Practical approach for this project:**
- Integrate **Steadfast** as your primary real carrier (fastest merchant registration, clean API)
- Use **Pathao sandbox** for OAuth2 integration testing
- Keep **Redx** as a mock adapter initially, upgrade later
- You don't need to be an actual registered business — merchant registration is straightforward and developers do it for testing regularly

---

### 1.4 Supported Use Cases — B2C, B2B, and C2C

| Model | Who | Example | How They Use ParcelTrack |
|---|---|---|---|
| **B2C** | Small seller → end buyer | Riya (Facebook clothes seller) | Dashboard + manual API call, shares tracking link in Messenger |
| **B2B** | Multi-courier merchant → end buyer via API | Shajgoj, mid-size fashion/electronics retailers on 3PLs | Programmatic API integration, receives outbound webhooks |
| **C2C** | Individual → individual | Bikroy.com seller shipping a used phone | Identical to B2C — same workflow, same account type |

**C2C is architecturally identical to B2C.** A Bikroy seller registers, ships via Steadfast, calls `POST /shipments`, shares the tracking link with the buyer. No separate architecture needed — just a different account tier in Keycloak. The system handles all three models with the same codebase.

---

### 1.5 User Tiers & API Key

**Three account tiers:**

```
Individual (B2C / C2C)
  - Riya, Bikroy seller, any single person
  - Uses: Simple dashboard to register shipments manually
  - Limits: 200 shipments/day
  - Auth: JWT (logs into dashboard) + API Key (if they want programmatic access)

Business (B2B)
  - Shajgoj, any merchant shipping via multiple third-party couriers
  - Uses: Pure API integration — no dashboard needed
  - Limits: 10,000 shipments/day
  - Auth: API Key only (machine-to-machine, no human login)
  - Extra: Outbound webhook URL registration (ParcelTrack calls their backend on status change)

Admin
  - You — platform management
  - Manages tenants, monitors system health
```

**What is the API Key and why does it exist?**

When a merchant's backend calls `POST /shipments` at 2am automatically, there is no human logging in — no browser, no password. The API Key is how your system answers three questions on every request:

1. **Who is calling?** → Identifies the tenant (maps to TenantId)
2. **Are they allowed?** → Validates the key is active and not revoked
3. **How much can they do?** → Enforces rate limits and daily shipment quotas per tier

JWT is for humans using a browser or dashboard. API Key is for code calling code.

**API Key storage:**
```
tenant_api_keys
- Id, TenantId, KeyHash (SHA-256, never store plaintext), 
- Prefix (first 8 chars shown in dashboard e.g. "ptk_live_"),
- CreatedAt, LastUsedAt, IsRevoked
```

The Gateway resolves the API Key to a TenantId on every request, injects it as a claim, and the rest of the system behaves identically whether the caller used JWT or API Key.

---

### 1.6 Complete User Workflows

**B2C / C2C — Individual Seller (Riya)**
```
1. Register at parceltrack.com → Keycloak creates account (Individual tier)
2. Dashboard shows API Key + daily usage
3. Ship parcel via Steadfast → get consignment ID "STD123456"
4. Call POST /shipments with API Key:
   { "trackingNumber": "STD123456", "carrier": "Steadfast", "buyerEmail": "buyer@gmail.com" }
5. Receive: { "trackingUrl": "parceltrack.com/track/STD123456/view" }
6. Paste URL into Messenger → buyer clicks anytime for live status
7. Buyer auto-receives email on every status change
8. Riya receives zero follow-up messages
```

**B2B — Business (Chaldal)**
```
1. Register at parceltrack.com → Keycloak creates account (Business tier)
2. Register their callback URL:
   POST /webhooks/register { "url": "https://chaldal.com/webhooks/parceltrack", "secret": "xxx" }
3. Integrate ParcelTrack API into their order management system
4. Customer places order on Chaldal → Chaldal ships via Steadfast
5. Chaldal's backend auto-calls POST /shipments (programmatic, no human)
6. Steadfast webhook fires → ParcelTrack processes status change
7. ParcelTrack calls Chaldal's registered callback:
   POST https://chaldal.com/webhooks/parceltrack
   { "orderId": "...", "trackingNumber": "...", "newStatus": "OutForDelivery" }
8. Chaldal notifies their customer their own way (their app, their SMS, their branding)
   — OR — passes buyerEmail to ParcelTrack and lets ParcelTrack email the buyer directly
```



### 2.1 High-Level Diagram

```
                          ┌─────────────────────────────────────────────────┐
                          │                  CLIENT LAYER                   │
                          │         (Swagger UI / Minimal Frontend)         │
                          └───────────────────┬─────────────────────────────┘
                                              │ HTTPS
                          ┌───────────────────▼─────────────────────────────┐
                          │              API GATEWAY (YARP)                  │
                          │   JWT validation via Keycloak JWKS endpoint      │
                          │   Rate limiting · Request routing · Logging      │
                          └────┬──────────┬──────────┬────────────┬──────────┘
                               │          │          │            │
               ┌───────────────▼┐  ┌──────▼───┐ ┌───▼──────┐ ┌──▼──────────┐
               │  Shipment Svc  │  │Tracking  │ │  Notif.  │ │   Identity  │
               │  .NET 8 Web API│  │  Svc     │ │   Svc    │ │  (Keycloak) │
               │  Clean Arch    │  │.NET 8    │ │ .NET 8   │ │  Docker     │
               │  EF Core+Dapper│  │Worker+API│ │  Worker  │ └─────────────┘
               └───────┬────────┘  └──────┬───┘ └───┬──────┘
                       │                  │          │
               ┌───────▼──────────────────▼──────────▼──────┐
               │                INFRASTRUCTURE               │
               │  PostgreSQL · Redis · Kafka · AWS Services  │
               └─────────────────────────────────────────────┘
```

### 2.2 Services Breakdown

| Service | Responsibility | Tech |
|---|---|---|
| **API Gateway** | Routing, JWT + API Key validation, rate limiting | YARP (.NET), Keycloak |
| **Shipment Service** | CRUD for shipments, business logic owner | .NET 8, EF Core, PostgreSQL |
| **Tracking Service** | Poll/receive carrier updates, publish events | .NET 8, Redis, Kafka |
| **Notification Service** | Consume events, send email + WebSocket to end buyers | .NET 8, SignalR, SendGrid |
| **Webhook Dispatch Service** | Consume events, call B2B business callback URLs | .NET 8, Kafka, Polly |
| **Keycloak** | Auth/Identity, JWT issuance, API Key tenant resolution | Docker container |

---

## 3. Service-by-Service Design

---

### 3.1 API Gateway

**Technology:** YARP (Yet Another Reverse Proxy) — native .NET, no need for Nginx or Kong.

**Responsibilities:**
- Route `/shipments/**` → Shipment Service
- Route `/tracking/**` → Tracking Service
- Validate Keycloak-issued JWT on every request (via JWKS endpoint)
- Global rate limiting (10 req/s per user via Redis sliding window)
- Correlation ID injection for distributed tracing
- Centralized request/response logging via Serilog → CloudWatch

**Key design decision:** No business logic lives here. It is purely infrastructure.

**Config pattern:**
```json
{
  "ReverseProxy": {
    "Routes": {
      "shipment-route": {
        "ClusterId": "shipment-cluster",
        "Match": { "Path": "/shipments/{**catch-all}" }
      }
    }
  }
}
```

---

### 3.2 Shipment Service

**Architecture:** Clean Architecture (strict layer separation)

```
ShipmentService/
├── Domain/
│   ├── Entities/          # Shipment, ShipmentEvent, Carrier
│   ├── Enums/             # ShipmentStatus, CarrierType
│   └── Exceptions/        # Domain exceptions
├── Application/
│   ├── Commands/          # CreateShipment, UpdateShipment, CancelShipment
│   ├── Queries/           # GetShipmentById, GetShipmentsByUser
│   ├── Interfaces/        # IShipmentRepository, ICarrierService
│   └── DTOs/
├── Infrastructure/
│   ├── Persistence/       # EF Core DbContext, Migrations, Repositories
│   ├── Carriers/          # Mock carrier API adapters
│   └── Messaging/         # Kafka producer
└── API/
    ├── Controllers/
    ├── Middleware/
    └── Program.cs
```

**CQRS with MediatR:** Every use case is a Command or Query. No fat controllers.

**Domain entities:**
```
Shipment
- Id (Guid)
- TrackingNumber (string, unique)
- CarrierType (enum: Steadfast, Pathao, Redx)
- Status (enum: Created, InTransit, OutForDelivery, Delivered, Failed)
- OriginAddress, DestinationAddress
- UserId (from JWT claim)
- TenantId (multi-tenant)
- CreatedAt, UpdatedAt
- Events: List<ShipmentEvent>

ShipmentEvent
- Id, ShipmentId, Status, Description, OccurredAt, Location
```

**Multi-tenancy approach:** TenantId extracted from JWT claim, injected via scoped service, applied as global EF Core query filter. No tenant can see another's data.

**Idempotency:** Apply your Redis idempotency pattern from work on `POST /shipments` — same pattern, your code, you own it fully.

**Endpoints:**
```
POST   /shipments              # Create shipment (auth required)
GET    /shipments/{id}         # Get by ID (auth required)
GET    /shipments              # List (paginated, filtered by status — auth required)
PUT    /shipments/{id}         # Update (auth required)
DELETE /shipments/{id}         # Cancel (auth required)
GET    /shipments/{id}/events  # Get tracking history (auth required)

# Public — no auth required
GET    /track/{trackingNumber} # Public tracking page / status endpoint
```

**Database:** PostgreSQL via EF Core. Use your own abstraction layer from work (rebuild it here — you own that pattern).

---

### 3.3 Public Tracking Page

This is how end users (buyers) check their parcel status themselves — no login required.

**Endpoint:**
```
GET /track/{trackingNumber}
```
Bypasses the API Gateway JWT validation — explicitly marked as anonymous in the Gateway routing config.

**Response (JSON):**
```json
{
  "trackingNumber": "TRK123456",
  "carrier": "Steadfast",
  "currentStatus": "OutForDelivery",
  "estimatedDelivery": "2025-03-10",
  "events": [
    { "status": "Created",         "description": "Parcel picked up from Dhaka",       "location": "Dhaka",      "occurredAt": "2025-03-09T10:00:00Z" },
    { "status": "InTransit",       "description": "In transit to Chattogram hub",       "location": "Comilla",    "occurredAt": "2025-03-09T15:00:00Z" },
    { "status": "OutForDelivery",  "description": "Out for delivery",                   "location": "Chattogram", "occurredAt": "2025-03-10T09:00:00Z" }
  ]
}
```

**Minimal HTML page:**

Also serve a simple `GET /track/{trackingNumber}/view` that returns a styled HTML page — no frontend framework needed, just a single Razor or static HTML response. This is what Riya sends to her buyer in Messenger:

```
https://parceltrack.com/track/TRK123456/view
```

The page shows:
```
📦 ParcelTrack — TRK123456
───────────────────────────────
✅ Mar 9, 10:00 AM  Parcel picked up · Dhaka
🚚 Mar 9,  3:00 PM  In transit · Comilla Hub
📬 Mar 10, 9:00 AM  Out for delivery · Chattogram
```

**The complete B2C seller workflow:**
1. Customer orders from Riya on Facebook → pays bKash
2. Riya ships via Steadfast → gets consignment ID
3. Riya calls `POST /shipments` with the tracking number (via ParcelTrack's API or a future simple dashboard)
4. ParcelTrack generates a shareable link automatically
5. Riya sends `parceltrack.com/track/TRK123456/view` to buyer in one Messenger message
6. Buyer can check anytime + receives automatic email/WebSocket notifications on every status change
7. Riya receives zero follow-up messages

**Security note:** The public endpoint only exposes tracking events — never the buyer's personal data, seller's tenant data, or any business information. TenantId is never returned in this response.

**Rate limiting on public endpoint:** Apply a stricter Redis rate limit on `/track/*` — 30 req/min per IP — to prevent scraping.

---

### 3.4 Tracking Service

This is the most architecturally interesting service. It has two responsibilities:

**A) Carrier Polling Worker (BackgroundService)**

Polls mock carrier APIs every N seconds for shipment status updates.

```
TrackingWorker (IHostedService)
  └── Every 30s:
        1. Fetch all active shipment tracking numbers from Redis (cached from DB)
        2. For each carrier, batch-call the mock carrier adapter
        3. Compare new status vs last known status (stored in Redis)
        4. If changed → publish ShipmentStatusChanged event to Kafka
        5. Update Redis cache with new status + TTL
```

**Why Redis here?**
- Avoid hitting PostgreSQL every 30 seconds for all active shipments
- Last-known status comparison is a O(1) Redis GET, not a DB query
- Cache key: `tracking:{trackingNumber}` → JSON of last known status

**B) Carrier Adapters — Real + Mock**

Design a `ICarrierAdapter` interface with implementations for each carrier:

```csharp
public interface ICarrierAdapter
{
    CarrierType CarrierType { get; }
    // Used by polling worker
    Task<CarrierTrackingResult> GetStatusAsync(string trackingNumber);
    // Used by webhook endpoint (null if carrier doesn't support webhooks)
    CarrierTrackingResult? ParseWebhookPayload(string rawPayload);
}

// Implementations:
// SteadfastAdapter  — real API (API Key auth), supports webhooks
// PathaoAdapter     — real API (OAuth2), supports webhooks, use sandbox
// RedxMockAdapter   — simulated, polling only (no real webhook support)
```

**SteadfastAdapter** calls the real API:
```
GET https://portal.steadfast.com.bd/api/v1/status_by_cid/{consignment_id}
Headers: Api-Key: xxx, Secret-Key: xxx
```

**PathaoAdapter** uses OAuth2 token flow first, then queries order status.

**RedxMockAdapter** simulates state machine: `Created → InTransit → OutForDelivery → Delivered`, advancing based on elapsed time since shipment creation. It randomly throws exceptions 10% of the time to force **circuit breaker** implementation (Polly).

**Polly policies to implement:**
- Retry with exponential backoff (3 retries)
- Circuit breaker (open after 5 consecutive failures, 30s break)
- Timeout policy (5s per carrier call)

This is exactly what interviewers ask about in system design.

**C) Webhook Endpoint (for Steadfast + Pathao)**

The Tracking Service also exposes a public webhook endpoint:
```
POST /webhooks/steadfast   # Steadfast posts here on status change
POST /webhooks/pathao      # Pathao posts here on status change
```

Each endpoint:
1. Validates the request signature (HMAC or shared secret)
2. Parses the payload via the carrier's `ParseWebhookPayload()`
3. Publishes `ShipmentStatusChanged` to Kafka directly — bypasses the polling worker entirely
4. Returns `200 OK` immediately (async processing)

**D) Kafka Event Published:**

```json
Topic: shipment.status.changed
{
  "shipmentId": "uuid",
  "trackingNumber": "TRK123",
  "tenantId": "uuid",
  "userId": "uuid",
  "previousStatus": "InTransit",
  "newStatus": "OutForDelivery",
  "location": "Dhaka Hub",
  "occurredAt": "2025-03-01T10:00:00Z"
}
```

---

### 3.5 Notification Service

**Consumes:** `shipment.status.changed` Kafka topic

**Sends:**
1. Email via SendGrid (free tier — 100 emails/day)
2. Real-time WebSocket push via SignalR

**Architecture:**

```
NotificationWorker (BackgroundService)
  └── Consumes Kafka message
        1. Deserialize ShipmentStatusChangedEvent
        2. Look up user notification preferences (PostgreSQL)
        3. Parallel fan-out:
           a. If email enabled → SendGrid
           b. If WebSocket connected → SignalR Hub push
        4. Store notification record in DB (for history endpoint)
        5. Commit Kafka offset AFTER successful processing
```

**Critical: Commit offset after processing, not before.** This ensures at-least-once delivery. Your notification logic must be idempotent (check if notification already sent for this event using a unique event ID).

**SignalR Hub:**
```csharp
// Client connects to: /hubs/notifications?access_token=<JWT>
public class NotificationHub : Hub
{
    // Groups by UserId — push only to the right user
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User.FindFirst("sub")?.Value;
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }
}
```

**Notification preferences table:**
```
UserNotificationPreference
- UserId, TenantId
- EmailEnabled (bool)
- WebSocketEnabled (bool)
- NotifyOnStatuses (array: e.g. ["OutForDelivery", "Delivered"])
```

---

### 3.6 Webhook Dispatch Service (B2B Outbound)

This service is what makes B2B work. When a shipment status changes, Business tier tenants need ParcelTrack to call *their* backend — not just email the end buyer.

**Consumes:** `shipment.status.changed` Kafka topic (same topic as Notification Service, different consumer group)

**Flow:**
```
WebhookDispatchWorker (BackgroundService)
  └── Consumes Kafka message
        1. Look up tenant by TenantId
        2. Check if tenant is Business tier AND has a registered callback URL
        3. If yes → POST to their callback URL with HMAC-signed payload
        4. Retry with exponential backoff on failure (Polly)
        5. After 5 failed attempts → publish to webhook.failed dead letter topic
        6. Log delivery attempt (success/failure) to DB
        7. Commit Kafka offset after processing
```

**Outbound payload to business:**
```json
POST https://chaldal.com/webhooks/parceltrack
Headers:
  X-ParcelTrack-Signature: sha256=<HMAC of body using their registered secret>
  X-ParcelTrack-Event: shipment.status.changed
  X-ParcelTrack-Delivery-Id: uuid

Body:
{
  "deliveryId": "uuid",
  "trackingNumber": "STD123456",
  "previousStatus": "InTransit",
  "newStatus": "OutForDelivery",
  "location": "Chattogram Hub",
  "occurredAt": "2025-03-10T09:00:00Z"
}
```

**HMAC Signature:** The business registers a secret when they set up their callback URL. ParcelTrack signs every outbound payload with that secret. The business verifies the signature on their end — this proves the call is genuinely from ParcelTrack, not a spoofed request. Exactly how Stripe and GitHub do it.

**Webhook registration endpoint (in Shipment Service):**
```
POST /webhooks/register
{ "callbackUrl": "https://chaldal.com/webhooks/parceltrack", "secret": "their_chosen_secret" }

GET  /webhooks/deliveries        # Delivery log — business can see success/failure history
POST /webhooks/redeliver/{id}    # Manually retry a failed delivery
```

**Delivery log table:**
```
webhook_deliveries
- Id, TenantId, ShipmentId, CallbackUrl
- Payload, ResponseStatusCode, ResponseBody
- AttemptCount, LastAttemptAt, Status (Pending/Delivered/Failed)
```

**Why a separate service and not inside Notification Service?**
Notification Service sends to end buyers (email + WebSocket) — always fire-and-forget, low stakes if slightly delayed. Webhook Dispatch sends to businesses' production systems — needs stricter retry logic, delivery guarantees, audit logs, and manual redelivery. Mixing them creates a single point of failure. Separate services, separate concerns.

---

### 3.7 Keycloak Setup

Run via Docker Compose alongside all services.

**Realm configuration:**
- Realm: `parceltrack`
- Client: `parceltrack-api` (confidential, client credentials for service-to-service)
- Client: `parceltrack-frontend` (public, for Swagger UI / test client)
- Roles: `user`, `admin`, `tenant-admin`
- Custom claim: `tenantId` mapped from user attribute

**JWT claims your services will use:**
```
sub         → userId
tenantId    → custom claim
realm_roles → for RBAC
```

You don't write any auth logic. Keycloak handles registration, login, token refresh, TOTP 2FA (you can enable it — you know how it works).

---

## 4. Infrastructure & Data Design

### 4.1 PostgreSQL Schema (Key Tables)

```sql
-- Shipment Service DB
shipments (id, tracking_number, carrier_type, status, user_id, tenant_id, ...)
shipment_events (id, shipment_id, status, description, location, occurred_at)
outbox_messages (id, aggregate_id, type, payload, created_at, processed_at) -- Transactional Outbox

-- Notification Service DB  
notifications (id, shipment_id, user_id, channel, status, sent_at)
user_notification_preferences (user_id, tenant_id, email_enabled, ws_enabled, notify_on_statuses)
```

### 4.2 Transactional Outbox Pattern

**Problem:** What if you save a shipment to DB but Kafka publish fails? Data is inconsistent.

**Solution:** Outbox pattern.

```
1. In same DB transaction:
   - INSERT INTO shipments
   - INSERT INTO outbox_messages (the Kafka event payload)
2. A background OutboxProcessor reads unprocessed outbox messages
3. Publishes to Kafka
4. Marks as processed
```

This guarantees exactly-once semantics between your DB write and Kafka publish. This is a senior-level pattern — put it explicitly on your resume.

### 4.3 Redis Usage Summary

| Key Pattern | Purpose | TTL |
|---|---|---|
| `tracking:{trackingNumber}` | Last known carrier status | 5 min |
| `idempotency:{requestId}` | Duplicate request prevention | 24 hr |
| `ratelimit:{userId}` | Sliding window rate limit | 1 min |
| `circuit:{carrierName}` | Circuit breaker state | Dynamic |

### 4.4 Kafka Topics

| Topic | Producer | Consumer | Partitions |
|---|---|---|---|
| `shipment.status.changed` | Tracking Svc | Notification Svc | 3 |
| `shipment.created` | Shipment Svc | Tracking Svc | 3 |
| `notification.failed` | Notification Svc | Dead Letter processor | 1 |

**Consumer groups:** Each service has its own consumer group. Notification Service uses `notification-service-group`.

---

## 5. AWS Deployment Architecture

### 5.1 Services Map

```
Route 53 (DNS)
  └── ALB (Application Load Balancer)
        └── ECS Fargate Cluster
              ├── API Gateway Task (2 instances)
              ├── Shipment Service Task (2 instances)
              ├── Tracking Service Task (1 instance — worker)
              ├── Notification Service Task (1 instance — worker)
              ├── Webhook Dispatch Service Task (1 instance — worker)
              └── Keycloak Task (1 instance)

RDS PostgreSQL (Multi-AZ for production — single AZ for your project)
ElastiCache Redis (single node)
MSK (Managed Kafka) — OR use a Kafka Docker container on EC2 to save cost
ECR (Container Registry) — stores your Docker images
CloudWatch — logs from all services via Serilog
S3 — store architecture diagrams, exports
Secrets Manager — DB passwords, SendGrid API key, Keycloak secrets
```

### 5.2 Cost Estimate (Dev/Demo)
- ECS Fargate (4 tasks, minimal sizing): ~$15–25/month
- RDS t3.micro: ~$15/month
- ElastiCache t3.micro: ~$13/month
- MSK is expensive — use a Kafka Docker container on the same EC2 instead: ~$10/month
- **Total: ~$50–60/month** — shut down when not demoing to save cost

### 5.3 Infrastructure as Code
Use **AWS CDK (C#)** — stays in your language, defines all AWS resources as code. This itself is a resume skill.

---

## 6. CI/CD Pipeline (GitHub Actions)

```yaml
# Trigger: push to main or PR
Pipeline:
  1. build-and-test
     - dotnet restore
     - dotnet build
     - dotnet test --collect:"XPlat Code Coverage"
     - Upload coverage to Codecov (free)
     - Fail if coverage < 80% on core services

  2. code-quality (parallel with tests)
     - SonarCloud scan (free for public repos)
     - You already know SonarQube from your resume — use SonarCloud

  3. docker-build-push (on main only)
     - docker build each service
     - Push to AWS ECR

  4. deploy (on main only, after docker step)
     - AWS CDK deploy
     - ECS service update (rolling deployment)
     - Health check validation
```

**Branch strategy:**
- `main` → production deploys
- `develop` → integration branch
- `feature/*` → individual features, PR into develop

---

## 7. Testing Strategy

### 7.1 Unit Tests (xUnit + Moq)

Write for every Application layer handler (Commands/Queries).

```csharp
// Example: CreateShipmentCommandHandlerTests
[Fact]
public async Task Handle_ValidCommand_ShouldCreateShipment()
{
    // Arrange
    var mockRepo = new Mock<IShipmentRepository>();
    var mockProducer = new Mock<IEventProducer>();
    var handler = new CreateShipmentCommandHandler(mockRepo.Object, mockProducer.Object);

    var command = new CreateShipmentCommand { TrackingNumber = "TRK001", CarrierType = CarrierType.DHL };

    // Act
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    mockRepo.Verify(r => r.AddAsync(It.IsAny<Shipment>()), Times.Once);
    mockProducer.Verify(p => p.PublishAsync("shipment.created", It.IsAny<object>()), Times.Once);
}
```

### 7.2 Integration Tests

Use `WebApplicationFactory<Program>` with:
- **Testcontainers** (Docker-based) for PostgreSQL and Redis — real DB, no mocks
- Seed data via fixtures
- Test full HTTP request → DB → response cycle

```csharp
// Testcontainers setup
var postgres = new PostgreSqlBuilder().Build();
await postgres.StartAsync();
// Pass connection string to WebApplicationFactory
```

### 7.3 Coverage Targets

| Service | Target |
|---|---|
| Shipment Service — Application layer | 90% |
| Tracking Service — Adapter + Worker logic | 80% |
| Notification Service — Consumer logic | 80% |
| Infrastructure layer | 50% (not worth mocking EF internals) |

---

## 8. Cross-Cutting Concerns

### 8.1 Structured Logging
Use **Serilog** across all services with:
- Correlation ID on every log entry (injected by Gateway)
- Structured JSON output → CloudWatch Logs
- Log levels: Info for business events, Warning for retries, Error for failures

### 8.2 Health Checks
Every service exposes `/health` and `/health/ready`:
- Liveness: is the process alive?
- Readiness: can it serve traffic? (checks DB, Redis, Kafka connectivity)
ECS uses these for rolling deployment health validation.

### 8.3 Distributed Tracing
Add **OpenTelemetry** with AWS X-Ray exporter. Trace a request from Gateway → Shipment Service → Kafka → Notification Service end-to-end. This is a talking point in interviews.

### 8.4 API Versioning
Use URL versioning: `/v1/shipments`. Even with one version, the pattern signals maturity.

---

## 9. Build Order (Week by Week)

| Week | Deliverable |
|---|---|
| 1 | Write architecture doc (this file). Draw diagram. Set up GitHub repo, branch strategy, solution structure |
| 2 | Docker Compose with Keycloak, PostgreSQL, Redis, Kafka running locally |
| 3 | Shipment Service — Domain + Application layer + all unit tests |
| 4 | Shipment Service — Infrastructure (EF Core, your abstraction layer) + API endpoints + integration tests |
| 5 | Tracking Service — Mock carrier adapters + Polly policies + unit tests |
| 6 | Tracking Service — Background polling worker + Redis caching + Kafka publish |
| 7 | Notification Service — Kafka consumer + SendGrid integration |
| 8 | Notification Service — SignalR hub + user preferences + idempotency |
| 9 | Outbox pattern in Shipment Service. API Gateway (YARP) with rate limiting |
| 10 | AWS CDK — provision all infrastructure. Deploy to AWS |
| 11 | GitHub Actions CI/CD pipeline. SonarCloud integration. Coverage gates |
| 12 | README polish, architecture diagram (use draw.io), Swagger docs, demo video |

---

## 10. Resume Bullet (Final Form)

> "Architected ParcelTrack — a multi-carrier parcel tracking platform integrating real Bangladeshi courier APIs (Steadfast, Pathao) with webhook + polling support, B2B outbound webhook dispatch with HMAC signing, built with .NET 8 microservices, Kafka-based notification fan-out (email + WebSocket), Redis caching + idempotency, Transactional Outbox pattern, Polly circuit breakers, Keycloak OAuth2 identity, deployed on AWS ECS Fargate with CDK IaC and full CI/CD via GitHub Actions. 80%+ test coverage via xUnit + Testcontainers."

Every word in that bullet is defensible in an interview because you built it.

---

## 11. Interview Talking Points This Project Unlocks

| Question | Your Answer |
|---|---|
| "Design a notification system" | You built one. Walk through Kafka fan-out, at-least-once delivery, idempotency |
| "How do you handle third-party API failures?" | Polly: retry + circuit breaker + dead letter queue |
| "What is the Transactional Outbox pattern?" | You implemented it. DB + outbox in same transaction, background processor publishes |
| "How do you ensure no duplicate processing?" | Redis idempotency key per requestId, TTL 24hr |
| "How does your system handle multi-tenancy?" | TenantId from JWT, EF Core global query filter, Keycloak claim mapping |
| "Walk me through your CI/CD pipeline" | GitHub Actions → build → test → coverage gate → Docker → ECR → ECS rolling deploy |
| "How do you notify B2B clients of events?" | Separate WebhookDispatchService — Kafka consumer, HMAC-signed outbound POST, Polly retry, dead letter queue, delivery audit log with manual redelivery |

---

*Architecture by: Chief Architect mode | Tailored for Mahir Hasan Sifat | March 2026*
