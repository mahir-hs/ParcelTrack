FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/Gateway/ParcelTrack.Gateway/ParcelTrack.Gateway.csproj \
     src/Gateway/ParcelTrack.Gateway/
RUN dotnet restore src/Gateway/ParcelTrack.Gateway/ParcelTrack.Gateway.csproj

COPY src/Gateway/ src/Gateway/
RUN dotnet publish src/Gateway/ParcelTrack.Gateway/ParcelTrack.Gateway.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ParcelTrack.Gateway.dll"]
