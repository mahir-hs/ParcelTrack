FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/Shared/ParcelTrack.Shared.Contracts/ParcelTrack.Shared.Contracts.csproj \
     src/Shared/ParcelTrack.Shared.Contracts/
COPY src/Services/TrackingService/ParcelTrack.TrackingService.Domain/ParcelTrack.TrackingService.Domain.csproj \
     src/Services/TrackingService/ParcelTrack.TrackingService.Domain/
COPY src/Services/TrackingService/ParcelTrack.TrackingService.Application/ParcelTrack.TrackingService.Application.csproj \
     src/Services/TrackingService/ParcelTrack.TrackingService.Application/
COPY src/Services/TrackingService/ParcelTrack.TrackingService.Infrastructure/ParcelTrack.TrackingService.Infrastructure.csproj \
     src/Services/TrackingService/ParcelTrack.TrackingService.Infrastructure/
COPY src/Services/TrackingService/ParcelTrack.TrackingService.Worker/ParcelTrack.TrackingService.Worker.csproj \
     src/Services/TrackingService/ParcelTrack.TrackingService.Worker/
RUN dotnet restore src/Services/TrackingService/ParcelTrack.TrackingService.Worker/ParcelTrack.TrackingService.Worker.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/TrackingService/ src/Services/TrackingService/
RUN dotnet publish src/Services/TrackingService/ParcelTrack.TrackingService.Worker/ParcelTrack.TrackingService.Worker.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ParcelTrack.TrackingService.Worker.dll"]
