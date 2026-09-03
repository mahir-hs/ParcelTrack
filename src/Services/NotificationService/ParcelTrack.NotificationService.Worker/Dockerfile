FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/Shared/ParcelTrack.Shared.Contracts/ParcelTrack.Shared.Contracts.csproj \
     src/Shared/ParcelTrack.Shared.Contracts/
COPY src/Services/NotificationService/ParcelTrack.NotificationService.Application/ParcelTrack.NotificationService.Application.csproj \
     src/Services/NotificationService/ParcelTrack.NotificationService.Application/
COPY src/Services/NotificationService/ParcelTrack.NotificationService.Worker/ParcelTrack.NotificationService.Worker.csproj \
     src/Services/NotificationService/ParcelTrack.NotificationService.Worker/
RUN dotnet restore src/Services/NotificationService/ParcelTrack.NotificationService.Worker/ParcelTrack.NotificationService.Worker.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/NotificationService/ src/Services/NotificationService/
RUN dotnet publish src/Services/NotificationService/ParcelTrack.NotificationService.Worker/ParcelTrack.NotificationService.Worker.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ParcelTrack.NotificationService.Worker.dll"]
