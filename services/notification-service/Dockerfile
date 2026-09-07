# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY SmartHotel.Notifications/SmartHotel.Notifications.API/SmartHotel.Notifications.API.csproj SmartHotel.Notifications/SmartHotel.Notifications.API/
COPY SmartHotel.Notifications/SmartHotel.Notifications.Application/SmartHotel.Notifications.Application.csproj SmartHotel.Notifications/SmartHotel.Notifications.Application/
COPY SmartHotel.Notifications/SmartHotel.Notifications.Domain/SmartHotel.Notifications.Domain.csproj SmartHotel.Notifications/SmartHotel.Notifications.Domain/
COPY SmartHotel.Notifications/SmartHotel.Notifications.Infrastructure/SmartHotel.Notifications.Infrastructure.csproj SmartHotel.Notifications/SmartHotel.Notifications.Infrastructure/
COPY SmartHotel.Notifications/SmartHotel.Notifications.sln SmartHotel.Notifications/

RUN dotnet restore SmartHotel.Notifications/SmartHotel.Notifications.API/SmartHotel.Notifications.API.csproj

# Copy all source files
COPY SmartHotel.Notifications/ SmartHotel.Notifications/

# Build and publish
WORKDIR /src/SmartHotel.Notifications/SmartHotel.Notifications.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5004
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5004

ENTRYPOINT ["dotnet", "SmartHotel.Notifications.API.dll"]
