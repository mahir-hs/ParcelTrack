FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/Shared/ParcelTrack.Shared.Contracts/ParcelTrack.Shared.Contracts.csproj \
     src/Shared/ParcelTrack.Shared.Contracts/
COPY src/Services/ShipmentService/ParcelTrack.ShipmentService.Domain/ParcelTrack.ShipmentService.Domain.csproj \
     src/Services/ShipmentService/ParcelTrack.ShipmentService.Domain/
COPY src/Services/ShipmentService/ParcelTrack.ShipmentService.Application/ParcelTrack.ShipmentService.Application.csproj \
     src/Services/ShipmentService/ParcelTrack.ShipmentService.Application/
COPY src/Services/ShipmentService/ParcelTrack.ShipmentService.Infrastructure/ParcelTrack.ShipmentService.Infrastructure.csproj \
     src/Services/ShipmentService/ParcelTrack.ShipmentService.Infrastructure/
COPY src/Services/ShipmentService/ParcelTrack.ShipmentService.API/ParcelTrack.ShipmentService.API.csproj \
     src/Services/ShipmentService/ParcelTrack.ShipmentService.API/
RUN dotnet restore src/Services/ShipmentService/ParcelTrack.ShipmentService.API/ParcelTrack.ShipmentService.API.csproj

COPY src/Shared/ src/Shared/
COPY src/Services/ShipmentService/ src/Services/ShipmentService/
RUN dotnet publish src/Services/ShipmentService/ParcelTrack.ShipmentService.API/ParcelTrack.ShipmentService.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ParcelTrack.ShipmentService.API.dll"]
