# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY SmartHotel.Booking/SmartHotel.Booking.API/SmartHotel.Booking.API.csproj SmartHotel.Booking/SmartHotel.Booking.API/
COPY SmartHotel.Booking/SmartHotel.Booking.Application/SmartHotel.Booking.Application.csproj SmartHotel.Booking/SmartHotel.Booking.Application/
COPY SmartHotel.Booking/SmartHotel.Booking.Domain/SmartHotel.Booking.Domain.csproj SmartHotel.Booking/SmartHotel.Booking.Domain/
COPY SmartHotel.Booking/SmartHotel.Booking.Infrastructure/SmartHotel.Booking.Infrastructure.csproj SmartHotel.Booking/SmartHotel.Booking.Infrastructure/
COPY SmartHotel.Booking/SmartHotel.Booking.sln SmartHotel.Booking/

RUN dotnet restore SmartHotel.Booking/SmartHotel.Booking.API/SmartHotel.Booking.API.csproj

# Copy all source files
COPY SmartHotel.Booking/ SmartHotel.Booking/

# Build and publish
WORKDIR /src/SmartHotel.Booking/SmartHotel.Booking.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5003
EXPOSE 5013

ENTRYPOINT ["dotnet", "SmartHotel.Booking.API.dll"]
