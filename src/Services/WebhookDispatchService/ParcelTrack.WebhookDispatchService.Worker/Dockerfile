FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/Shared/ParcelTrack.Shared.Contracts/ParcelTrack.Shared.Contracts.csproj \
     src/Shared/ParcelTrack.Shared.Contracts/
COPY src/Services/WebhookDispatchService/ParcelTrack.WebhookDispatchService.Worker/ParcelTrack.WebhookDispatchService.Worker.csproj \
     src/Services/WebhookDispatchService/ParcelTrack.WebhookDispatchService.Worker/
RUN dotnet restore src/Services/WebhookDispatchService/ParcelTrack.WebhookDispatchService.Worker/ParcelTrack.WebhookDispatchService.Worker.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/WebhookDispatchService/ src/Services/WebhookDispatchService/
RUN dotnet publish src/Services/WebhookDispatchService/ParcelTrack.WebhookDispatchService.Worker/ParcelTrack.WebhookDispatchService.Worker.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ParcelTrack.WebhookDispatchService.Worker.dll"]
