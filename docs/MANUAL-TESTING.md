# Manual end-to-end test

Unit tests prove the pieces. This proves the wiring: a courier reports movement, and that
observation travels through Kafka, gets validated by the shipment state machine, and comes
back out as an email and a webhook.

Everything below runs locally. No merchant account needed.

---

## 1. Prerequisites

- PostgreSQL running natively
- Docker Desktop running
- A `.env` at the repo root (copy `.env.example`, fill in the passwords)

Create the databases once:

```sql
CREATE DATABASE parceltrack_shipment;
CREATE DATABASE parceltrack_notification;
CREATE DATABASE parceltrack_tracking;
CREATE DATABASE parceltrack_webhook;
CREATE DATABASE parceltrack_keycloak;
```

## 2. Start the infrastructure

```powershell
docker-compose --profile messaging --profile auth up -d
docker ps
```

Wait for Keycloak — first boot imports the realm and takes 30–60s. It's ready when
`http://localhost:8180` loads.

Confirm the Kafka topics were created:

```powershell
docker exec parceltrack-kafka kafka-topics --bootstrap-server localhost:9092 --list
```

You want to see `shipment.created`, `shipment.status.changed`, and `carrier.status.observed`.

> If `carrier.status.observed` is missing, your Kafka volume predates it:
> `docker exec parceltrack-kafka kafka-topics --bootstrap-server localhost:9092 --create --topic carrier.status.observed --partitions 3 --replication-factor 1`

## 3. Apply migrations

```powershell
dotnet ef database update `
  --project src/Services/ShipmentService/ParcelTrack.ShipmentService.Infrastructure `
  --startup-project src/Services/ShipmentService/ParcelTrack.ShipmentService.API
```

Tracking and Webhook workers migrate themselves on startup.

## 4. Run the services

Four terminals. Watching the logs *is* the test — most of what you're verifying is
services reacting to each other.

```powershell
# 1
dotnet run --project src/Services/ShipmentService/ParcelTrack.ShipmentService.API

# 2
dotnet run --project src/Services/TrackingService/ParcelTrack.TrackingService.Worker --urls http://localhost:5072

# 3
dotnet run --project src/Services/NotificationService/ParcelTrack.NotificationService.Worker

# 4  (optional — only if you want to see outbound webhooks)
dotnet run --project src/Services/WebhookDispatchService/ParcelTrack.WebhookDispatchService.Worker --urls http://localhost:5070
```

In terminal 1 you should see the outbox processor and the carrier observation consumer start.
In terminal 2, the Kafka consumer and the polling worker.

## 5. Get a token

The realm ships with test users. `riya-test` is the small-seller persona.

```powershell
$token = (Invoke-RestMethod -Uri 'http://localhost:8180/realms/parceltrack/protocol/openid-connect/token' `
  -Method Post -ContentType 'application/x-www-form-urlencoded' -Body @{
    grant_type = 'password'
    client_id  = 'parceltrack-frontend'
    username   = 'riya-test'
    password   = 'Riya@1234'
  }).access_token

$headers = @{ Authorization = "Bearer $token" }
$token.Length   # non-zero means you have a token
```

| User | Password | Tenant |
|---|---|---|
| `riya-test` | `Riya@1234` | `...0002` |
| `chaldal-test` | `Chaldal@1234` | `...0003` |
| `admin-user` | `Admin@1234` | `...0001` |

Paste the token into [jwt.io](https://jwt.io) if you want to see the `tenantId` claim the
whole multi-tenancy story depends on.

## 6. Create a shipment

```powershell
$tracking = "PT-" + (Get-Random -Minimum 100000 -Maximum 999999)

$shipment = Invoke-RestMethod -Uri 'http://localhost:5068/v1/shipments' -Method Post `
  -Headers ($headers + @{ 'X-Idempotency-Key' = [guid]::NewGuid().ToString() }) `
  -ContentType 'application/json' `
  -Body (@{
      trackingNumber  = $tracking
      carrierType     = 'Pathao'
      buyerEmail      = 'buyer@example.com'
      destinationCity = 'Dhaka'
  } | ConvertTo-Json)

$shipment.id
$shipment.status      # Created
```

**What to look for in the logs:** ShipmentService writes an outbox row, the outbox processor
publishes `shipment.created` within ~5s, and TrackingService logs
`Registered PT-xxxxxx for Pathao polling`. That last line means the parcel is now in the
polling registry.

**Try the idempotency guard** — send the exact same request with the *same*
`X-Idempotency-Key` and you get the original shipment back rather than a duplicate.

## 7. Simulate the courier

This is the interesting part. The Pathao sandbox doesn't know about a tracking number you
invented, so instead of waiting for a poll, play the courier yourself. This hits the same
code path a real Pathao push would — `ParseWebhookPayload` → `CarrierObservationApplier` →
Kafka → ShipmentService.

```powershell
Invoke-RestMethod -Uri 'http://localhost:5072/webhooks/pathao' -Method Post `
  -ContentType 'application/json' `
  -Body (@{
      consignment_id    = $tracking
      order_status      = 'In Transit'
      order_status_slug = 'In_Transit'
      updated_at        = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
  } | ConvertTo-Json)
```

Expect `outcome: Applied`.

**Now watch all four terminals in order:**

1. **TrackingService** — `PT-xxxxxx: Created → InTransit (from Pathao)`, then publishes
   `carrier.status.observed`
2. **ShipmentService** — `Applied Pathao observation to shipment {id}: now InTransit`
3. **ShipmentService** — outbox publishes `shipment.status.changed`
4. **NotificationService** — sends (or logs) the buyer email
5. **TrackingService** again — consumes its own round-trip and appends to the tracking log

That chain is the entire system working.

## 8. Verify both views agree

```powershell
# The authoritative shipment
(Invoke-RestMethod -Uri "http://localhost:5068/v1/shipments/$($shipment.id)" -Headers $headers).status

# The public tracking page — no auth
Invoke-RestMethod -Uri "http://localhost:5068/v1/track/$tracking"
```

Both should say `InTransit`.

**This is the bug that was fixed last.** Before the carrier observation consumer existed, the
tracking view would have moved and the shipment would still have said `Created`.

Walk it forward through the rest of the lifecycle:

```powershell
foreach ($s in 'Assigned_for_Delivery', 'Delivered') {
    Invoke-RestMethod -Uri 'http://localhost:5072/webhooks/pathao' -Method Post `
      -ContentType 'application/json' `
      -Body (@{
          consignment_id    = $tracking
          order_status      = $s
          order_status_slug = $s
          updated_at        = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
      } | ConvertTo-Json)
    Start-Sleep -Seconds 3
}

Invoke-RestMethod -Uri "http://localhost:5068/v1/shipments/$($shipment.id)/events" -Headers $headers
```

## 9. Prove the domain still rules

The interesting tests are the ones that *should* fail.

**A courier claiming the impossible.** Create a second shipment and immediately tell
ParcelTrack it was delivered — `Created → Delivered` is not a legal transition:

```powershell
# ... create $tracking2 as in step 6, then:
Invoke-RestMethod -Uri 'http://localhost:5072/webhooks/pathao' -Method Post `
  -ContentType 'application/json' `
  -Body (@{ consignment_id = $tracking2; order_status = 'Delivered'; order_status_slug = 'Delivered' } | ConvertTo-Json)
```

The webhook returns `Applied` (TrackingService accepted the observation), but ShipmentService
logs `Ignoring impossible transition to Delivered` and the shipment stays `Created`. The
courier does not get to overrule the state machine — **and the consumer stays alive**, which
is the part that matters.

**A repeated status.** Send `In_Transit` twice. The second returns `NoChange` and publishes
nothing — this is what stops a buyer being emailed every 30 seconds by the poller.

**An unknown consignment.** Post a webhook for a tracking number that was never registered.
Returns `NotTracked` with a 2xx, because a 4xx would make the courier retry forever.

**Tenant isolation.** Get a token for `chaldal-test` and request Riya's shipment by id:

```powershell
Invoke-RestMethod -Uri "http://localhost:5068/v1/shipments/$($shipment.id)" -Headers $otherHeaders
```

404, not 403 — the global query filter means the row is invisible to another tenant, so the
service genuinely cannot find it.

## 10. Optional: poll the real Pathao sandbox

Steps 7–9 simulate the courier. To watch a real poll cycle instead, create an order in the
Pathao sandbox and register *its* consignment id with ParcelTrack:

```powershell
$pathaoToken = (Invoke-RestMethod -Uri 'https://courier-api-sandbox.pathao.com/aladdin/api/v1/issue-token' `
  -Method Post -ContentType 'application/json' -Body (@{
      client_id = '7N1aMJQbWm'
      client_secret = 'wRcaibZkUdSNz2EI9ZyuXLlNrnAv0TdPUPXMnD39'
      username = 'test@pathao.com'
      password = 'lovePathao'
      grant_type = 'password'
  } | ConvertTo-Json)).access_token
```

Then create an order through their API and use the returned `consignment_id` as the
`trackingNumber` in step 6. Within 30s the polling worker will fetch its real status.

Note that sandbox orders tend to sit at `Pickup_Requested`, which maps to `Created` — so
you'll see the poll happen in the logs but no status change, because nothing changed. That is
correct behaviour, and exactly why step 7 is the better demo.

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| 401 on every shipment call | Token expired (5 min default) — get a new one |
| `JWT is missing the 'tenantId' claim` | Used `parceltrack-api` instead of `parceltrack-frontend` as `client_id` |
| Shipment created but TrackingService silent | Outbox publishes every 5s — wait, then check Kafka is healthy |
| `Registered ... for polling` never appears | `carrierType` must be `Pathao`, `Steadfast`, or `Redx` |
| Webhook returns `NotTracked` | The consignment id doesn't match the `trackingNumber` you registered |
| Poll cycle never runs | `Polling:Enabled` is false outside Development — that's deliberate |
| Nothing at all on `:5072` | Pass `--urls http://localhost:5072`, the worker has no launch profile for it |
