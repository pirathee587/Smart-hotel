# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY shared/SmartHotel.Authorization/SmartHotel.Authorization.csproj shared/SmartHotel.Authorization/
COPY services/hotel-ops-service/SmartHotel.HotelOps/SmartHotel.HotelOps.API/SmartHotel.HotelOps.API.csproj services/hotel-ops-service/SmartHotel.HotelOps/SmartHotel.HotelOps.API/
COPY services/hotel-ops-service/SmartHotel.HotelOps/SmartHotel.HotelOps.Application/SmartHotel.HotelOps.Application.csproj services/hotel-ops-service/SmartHotel.HotelOps/SmartHotel.HotelOps.Application/
COPY services/hotel-ops-service/SmartHotel.HotelOps/SmartHotel.HotelOps.Domain/SmartHotel.HotelOps.Domain.csproj services/hotel-ops-service/SmartHotel.HotelOps/SmartHotel.HotelOps.Domain/
COPY services/hotel-ops-service/SmartHotel.HotelOps/SmartHotel.HotelOps.Infrastructure/SmartHotel.HotelOps.Infrastructure.csproj services/hotel-ops-service/SmartHotel.HotelOps/SmartHotel.HotelOps.Infrastructure/

RUN dotnet restore services/hotel-ops-service/SmartHotel.HotelOps/SmartHotel.HotelOps.API/SmartHotel.HotelOps.API.csproj

# Copy all source files
COPY shared/SmartHotel.Authorization/ shared/SmartHotel.Authorization/
COPY services/hotel-ops-service/SmartHotel.HotelOps/ services/hotel-ops-service/SmartHotel.HotelOps/

# Build and publish
WORKDIR /src/services/hotel-ops-service/SmartHotel.HotelOps/SmartHotel.HotelOps.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5002
EXPOSE 5012
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD curl --fail --silent http://localhost:5002/health || exit 1
ENTRYPOINT ["dotnet", "SmartHotel.HotelOps.API.dll"]
