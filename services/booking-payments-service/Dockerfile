# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY shared/SmartHotel.Authorization/SmartHotel.Authorization.csproj shared/SmartHotel.Authorization/
COPY services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.API/SmartHotel.Booking.API.csproj services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.API/
COPY services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.Application/SmartHotel.Booking.Application.csproj services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.Application/
COPY services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.Domain/SmartHotel.Booking.Domain.csproj services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.Domain/
COPY services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.Infrastructure/SmartHotel.Booking.Infrastructure.csproj services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.Infrastructure/

RUN dotnet restore services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.API/SmartHotel.Booking.API.csproj

# Copy all source files
COPY shared/SmartHotel.Authorization/ shared/SmartHotel.Authorization/
COPY services/booking-payments-service/SmartHotel.Booking/ services/booking-payments-service/SmartHotel.Booking/

# Build and publish
WORKDIR /src/services/booking-payments-service/SmartHotel.Booking/SmartHotel.Booking.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5003
EXPOSE 5013
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD curl --fail --silent http://localhost:5003/health || exit 1
ENTRYPOINT ["dotnet", "SmartHotel.Booking.API.dll"]
