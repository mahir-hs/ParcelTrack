# ParcelTrack — Project Context for Claude

## Who I Am
- Name: Mahir Hasan Sifat
- Role: .NET / ASP.NET Core backend developer
- Machine: ASUS VivoBook (hostname: AZKABAN), Windows 11 Home
- Tools: Visual Studio 2022, Docker Desktop

## What This Project Is
ParcelTrack — a multi-carrier parcel tracking & notification SaaS platform for Bangladeshi couriers (Steadfast, Pathao, Redx). Backend-heavy. Minimal UI (Swagger is fine). Full architecture plan is in the uploaded file `ParcelTrack-Architecture-Plan.md`.

## Current Status
- ✅ Week 1 DONE — Solution structure created and pushed to GitHub
- 🔜 Week 2 NEXT — Docker Compose with Keycloak, PostgreSQL, Redis, Kafka running locally

## Solution Structure (Already Created)
```
ParcelTrack/
├── src/
│   ├── Gateway/
│   │   └── ParcelTrack.Gateway                          (webapi)
│   ├── Services/
│   │   ├── ShipmentService/
│   │   │   ├── ParcelTrack.ShipmentService.Domain       (classlib)
│   │   │   ├── ParcelTrack.ShipmentService.Application  (classlib)
│   │   │   ├── ParcelTrack.ShipmentService.Infrastructure (classlib)
│   │   │   └── ParcelTrack.ShipmentService.API          (webapi)
│   │   ├── TrackingService/
│   │   │   ├── ParcelTrack.TrackingService.Domain       (classlib)
│   │   │   ├── ParcelTrack.TrackingService.Application  (classlib)
│   │   │   ├── ParcelTrack.TrackingService.Infrastructure (classlib)
│   │   │   └── ParcelTrack.TrackingService.Worker       (worker)
│   │   ├── NotificationService/
│   │   │   ├── ParcelTrack.NotificationService.Application (classlib)
│   │   │   └── ParcelTrack.NotificationService.Worker   (worker)
│   │   └── WebhookDispatchService/
│   │       └── ParcelTrack.WebhookDispatchService.Worker (worker)
│   └── Shared/
│       ├── ParcelTrack.Shared.Contracts                 (classlib)
│       └── ParcelTrack.Shared.Common                    (classlib)
└── tests/
    ├── ShipmentService/
    │   ├── ParcelTrack.ShipmentService.UnitTests        (xunit)
    │   └── ParcelTrack.ShipmentService.IntegrationTests (xunit)
    ├── TrackingService/
    │   └── ParcelTrack.TrackingService.UnitTests        (xunit)
    └── NotificationService/
        └── ParcelTrack.NotificationService.UnitTests    (xunit)
```

Note: `infra/` (AWS CDK C# project) will be added in Week 10.

## Branch Strategy
- `main` → production deploys
- `develop` → integration branch  
- `feature/*` → individual features, PR into develop

## Key Tech Decisions Already Made
- **API Gateway:** YARP (.NET native reverse proxy)
- **Auth:** Keycloak (Docker container) — JWT for humans, API Key for machine-to-machine
- **Architecture:** Clean Architecture + CQRS with MediatR in Shipment Service
- **DB:** PostgreSQL via EF Core + Dapper
- **Messaging:** Kafka
- **Cache:** Redis
- **Notifications:** SendGrid (email) + SignalR (WebSocket)
- **Deployment:** AWS ECS Fargate + CDK (C#) IaC
- **CI/CD:** GitHub Actions → ECR → ECS rolling deploy
- **Tests:** xUnit + Moq + Testcontainers
- **Logging:** Serilog → CloudWatch

## Concepts Already Explained (Don't Re-Explain)
- What ECS Fargate is
- What the API Gateway is for
- Project structure rationale
- Free deployment alternatives (Railway, Oracle Cloud Free Tier, Render, Fly.io)
- Why AWS matters for this resume project

## How to Continue
Always follow the week-by-week build order from the architecture doc (Section 9). Ask me to confirm current week before starting. Give step-by-step instructions I can follow from scratch — I had not started this project before this chat.
