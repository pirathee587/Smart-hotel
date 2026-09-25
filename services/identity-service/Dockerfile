# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY shared/SmartHotel.Authorization/SmartHotel.Authorization.csproj shared/SmartHotel.Authorization/
COPY services/identity-service/SmartHotel.Identity/SmartHotel.Identity.API/SmartHotel.Identity.API.csproj services/identity-service/SmartHotel.Identity/SmartHotel.Identity.API/
COPY services/identity-service/SmartHotel.Identity/SmartHotel.Identity.Application/SmartHotel.Identity.Application.csproj services/identity-service/SmartHotel.Identity/SmartHotel.Identity.Application/
COPY services/identity-service/SmartHotel.Identity/SmartHotel.Identity.Domain/SmartHotel.Identity.Domain.csproj services/identity-service/SmartHotel.Identity/SmartHotel.Identity.Domain/
COPY services/identity-service/SmartHotel.Identity/SmartHotel.Identity.Grpc/SmartHotel.Identity.Grpc.csproj services/identity-service/SmartHotel.Identity/SmartHotel.Identity.Grpc/
COPY services/identity-service/SmartHotel.Identity/SmartHotel.Identity.Infrastructure/SmartHotel.Identity.Infrastructure.csproj services/identity-service/SmartHotel.Identity/SmartHotel.Identity.Infrastructure/

RUN dotnet restore services/identity-service/SmartHotel.Identity/SmartHotel.Identity.API/SmartHotel.Identity.API.csproj

# Copy all source files
COPY shared/SmartHotel.Authorization/ shared/SmartHotel.Authorization/
COPY services/identity-service/SmartHotel.Identity/ services/identity-service/SmartHotel.Identity/

# Build and publish
WORKDIR /src/services/identity-service/SmartHotel.Identity/SmartHotel.Identity.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

# Create the RSA keys directory and hand ownership to the non-root app user
# before switching to that user. APP_UID=1654 in the official dotnet images.
RUN mkdir -p /app/keys && chown "$APP_UID:$APP_UID" /app/keys && chmod 700 /app/keys

ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5001
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 CMD curl --fail --silent http://localhost:5001/health || exit 1
ENTRYPOINT ["dotnet", "SmartHotel.Identity.API.dll"]
