# Code tour — understanding what you built

A reading order for ParcelTrack, arranged so each stop makes sense given the one before it.
Roughly six sittings. Read the code open beside this.

Each stop ends with **"you should be able to answer"** — questions in the shape interviewers
actually ask. If one makes you hesitate, that's the part to reread.

---

## Sitting 1 — The shape of the thing

**Read:** the `.csproj` files under `src/Services/ShipmentService/`, just the
`<ProjectReference>` lines.

Four projects, and the references only ever point one direction:

```
Domain  ←  Application  ←  Infrastructure  ←  API
```

Domain references nothing. Application references Domain. Infrastructure references
Application. API references all three.

**Why it matters:** business rules can't depend on EF Core, Kafka, or HTTP. `Shipment` doesn't
know a database exists. That's what makes the domain testable without any infrastructure —
and it's enforced by the compiler, not by discipline. Try adding a `using Microsoft.EntityFrameworkCore`
to the Domain project and watch it fail.

Notice `Application/Interfaces/` — `IShipmentRepository`, `IEventProducer`, `IUnitOfWork`.
Application *declares* what it needs; Infrastructure *implements* it. The dependency arrow
points inward even though the data flows outward. That inversion is the whole point of the
pattern.

**You should be able to answer:** why is `IShipmentRepository` defined in Application but
implemented in Infrastructure? What breaks if you move it?

---

## Sitting 2 — The domain owns the rules

**Read:** `src/Services/ShipmentService/ParcelTrack.ShipmentService.Domain/Entities/Shipment.cs`

Read it top to bottom; it's the most important file in the repo.

- **Line 11** — `AllowedTransitions`, the state machine as data. Every legal move in one place
  you can read in ten seconds.
- **Lines 22–32** — every setter is `private`. You cannot assign `Status` from outside.
- **Line 38** — private constructor. **Line 41** — `Create(...)` factory. There is exactly one
  way to bring a shipment into existence, and it enforces its own invariants.
- **Line 75** — `UpdateStatus`. Checks terminal state, checks the transition table, enforces
  the delivery-attempt cap. Three rules, one place.

**The idea:** an object that cannot be put into an invalid state. Not "we validate in the
service layer" — the type system and the aggregate refuse. Any caller, from anywhere, gets
the same enforcement. That's why the carrier consumer you wrote last could reuse this for
free: the courier goes through the same door as the API.

**You should be able to answer:** where would you add a `Returned` status, and what would
break? Why is `DeliveryAttempts` incremented inside the entity rather than by the handler?

---

## Sitting 3 — A request, end to end

**Read, in this order:**

1. `API/Controllers/ShipmentsController.cs` — thin. Binds a request, calls a handler, maps the
   result. No business logic, and note that `TenantId` is never read from the body.
2. `Application/Handler/CreateShipmentCommandHandler.cs` — orchestration only: check
   idempotency, build the aggregate, publish, save.
3. `Application/Handler/UpdateShipmentStatusCommandHandler.cs` — load, delegate to the domain,
   publish, save.

**The idea:** handlers are plain scoped classes. No MediatR, no `IRequestHandler<,>`, no
pipeline behaviours. A controller calls a method on a class. The stack trace tells the truth,
and there's one less layer of indirection between the route and the work.

Notice what handlers *don't* do: no `WHERE TenantId = ...`, no Kafka call, no transaction
management. Each of those is handled somewhere the handler doesn't have to think about, which
is the next three sittings.

**You should be able to answer:** why does `UpdateShipmentStatusCommandHandler` capture
`previousStatus` before calling `UpdateStatus`? What would go wrong if it read it after?

---

## Sitting 4 — The outbox

The pattern most worth being able to explain.

**Read:**
1. `Infrastructure/Messaging/OutboxEventProducer.cs` — the write side. Note the comment "No
   I/O". It adds a row to the *same* `DbContext` the handler is using, then returns.
2. `Infrastructure/BackgroundServices/OutboxProcessor.cs` — the read side, particularly
   **lines 75–90**.

**The problem it solves.** A handler updates a shipment and publishes an event. Two systems,
no shared transaction. Publish first and the database write may fail — you've announced
something that never happened. Write first and the publish may fail — the parcel moved and
nobody was told.

**The fix:** the event becomes a row in the same database, written in the same transaction. It
cannot be half-done. A background processor then drains that table into Kafka, retrying until
it succeeds or hits `MaxAttempts = 5` and dead-letters.

**`FOR UPDATE SKIP LOCKED`** (line 86) is what lets you run more than one API instance. Each
processor locks the rows it takes; other instances *skip* those rows rather than blocking, so
three instances process three disjoint batches instead of fighting over one.

**You should be able to answer:** what exactly is guaranteed here — at-least-once or
exactly-once delivery? What must consumers therefore do? (The answer is why the poller checks
whether a status actually changed.)

---

## Sitting 5 — Multi-tenancy

**Read:**
1. `Infrastructure/Persistence/ShipmentDbContext.cs`, `OnModelCreating` — the
   `HasQueryFilter` line.
2. `API/Infrastructure/TenantContext.cs` — the whole file.
3. `Application/Interfaces/ITenantContextSetter.cs`.

**The idea:** one line in `OnModelCreating` scopes *every* shipment query to the current
tenant. Not "remember to add a WHERE clause" — a developer who forgets still can't leak data,
because the filter is applied at the model level to every SELECT EF generates.

`TenantContext` resolves the tenant two ways: from JWT claims for API calls, or from an
explicit assignment for background work. That second path exists because of a real problem you
hit — a Kafka consumer has no HTTP request and therefore no claims, but every query still
needs a tenant. The message carries one, so the consumer states it.

Notice `ITenantContextSetter` is a *separate interface* from `ITenantContext`. Handlers get
the read side only and cannot reassign the tenant mid-request. Only infrastructure that owns a
scope's lifetime takes the write side.

**You should be able to answer:** if another tenant requests your shipment by id, do they get
403 or 404, and why? What would break if `TenantContext` were registered as a singleton?

---

## Sitting 6 — The carrier layer

The newest and most self-contained part.

**Read, in order:**
1. `TrackingService.Application/Interfaces/ICarrierAdapter.cs` — the contract. Two methods,
   because status arrives two ways.
2. `TrackingService.Infrastructure/Carriers/Pathao/PathaoStatusMapper.cs` — vocabulary
   translation, and why unknown maps to `Unknown` instead of throwing.
3. `.../Pathao/PathaoTokenProvider.cs` — token caching, early refresh, and the semaphore that
   makes a burst of callers produce one token request.
4. `.../Pathao/PathaoAdapter.cs` — bearer auth, the 401-retry-once path, 404 → `null`.
5. `TrackingService.Infrastructure/Extensions/CarrierExtensions.cs` — retry, circuit breaker,
   timeout, and why they're on the HttpClient rather than in the adapter.

**The idea — anti-corruption layer.** Pathao's JSON, OAuth2 flow, status names, and quirks stop
at this boundary. Everything past it sees a `CarrierTrackingResult` with a normalised status.
Adding Steadfast means writing one class; nothing downstream changes.

**Note the distinction between an answer and a fault.** A 404 means "no such parcel" — a
normal answer, returned as `null`. A 500 is a fault, thrown, retried, and counted toward the
circuit breaker. Confusing the two would have the breaker trip on parcels that simply aren't
booked yet.

**You should be able to answer:** why is resilience configured in DI rather than inside
`PathaoAdapter`? Why does the token provider re-check the cache *after* acquiring the lock?

---

## Sitting 7 — Observations vs. decisions

The most interesting design decision in the project, and the one worth rehearsing.

**Read:**
1. `Shared.Contracts/Events/CarrierStatusObservedEvent.cs` — read the doc comment first.
2. `TrackingService.Application/Services/CarrierObservationApplier.cs`
3. `TrackingService.Application/Services/CarrierPollingService.cs`
4. `ShipmentService.Application/Handler/ApplyCarrierObservationHandler.cs`
5. `ShipmentService.Infrastructure/BackgroundServices/CarrierObservationConsumer.cs`

**The idea:** two event types that look interchangeable and are not.

- `carrier.status.observed` — a courier *claims* something. Unvalidated, possibly impossible.
- `shipment.status.changed` — ParcelTrack *decided* something is true, after the state machine
  agreed.

Collapsing them into one would have caused an infinite loop: the poller publishes, ShipmentService
consumes and applies, which publishes the same event, which the poller's service consumes...

The courier's claim goes through `UpdateShipmentStatusCommandHandler` — the same handler an API
call uses. A courier claiming a brand-new parcel is `Delivered` gets rejected exactly as a
client would be.

**Note that rejections are logged, never thrown.** A Kafka consumer that throws on a message it
can never process will retry it forever and block every message behind it on that partition.
An impossible transition is bad data, not a transient fault, so it's recorded and skipped.

**You should be able to answer:** why not have the poller publish `shipment.status.changed`
directly? Why does the consumer commit offsets manually, after handling, rather than
auto-committing?

---

## Sitting 8 — The supporting cast

Shorter stops, each one idea:

| Read | The idea |
|---|---|
| `Infrastructure/Cache/RedisIdempotencyService.cs` | A client retry must not create a second parcel. Keyed on a client-supplied header. |
| `WebhookDispatchService/Application/WebhookDispatchHandler.cs` | HMAC signing so tenants can verify the call is yours; exponential backoff; every attempt audited. |
| `NotificationService.Worker/Worker.cs` | A Kafka consumer at its simplest — compare with the carrier consumer you wrote later. |
| `Gateway/Program.cs` | ~50 lines. Routing and rate limiting are configuration, not code. |
| `Infrastructure/Persistence/Configurations/*.cs` | Mapping lives beside the entity it maps, applied by assembly scan. |

---

## What to do with this

Once the questions above are comfortable, write your own answers to these three out loud —
they're the ones this project earns you the right to answer from experience:

1. **"Design a notification system."** Walk the flow: API → outbox → Kafka → three independent
   consumers. Explain why the API doesn't call Kafka directly.
2. **"How do you handle third-party API failures?"** Timeout inside retry inside circuit
   breaker, and the distinction between a 404 answer and a 500 fault.
3. **"How does multi-tenancy work?"** JWT claim → `ITenantContext` → global query filter, and
   the background-work problem that `ITenantContextSetter` solves.

The honest framing for the fourth one, if asked what you'd do differently: the `tracked_shipments`
table duplicates state that could be derived from the tracking log, and a Redis cache in front
of the poll cycle was the original plan. It's a deliberate trade — one source of truth over
one fewer table — and being able to say why you chose it is worth more than having chosen the
other one.
