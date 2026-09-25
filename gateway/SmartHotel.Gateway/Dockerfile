# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY shared/SmartHotel.Authorization/SmartHotel.Authorization.csproj shared/SmartHotel.Authorization/
COPY gateway/SmartHotel.Gateway/SmartHotel.Gateway.csproj gateway/SmartHotel.Gateway/
RUN dotnet restore gateway/SmartHotel.Gateway/SmartHotel.Gateway.csproj
COPY shared/SmartHotel.Authorization/ shared/SmartHotel.Authorization/
COPY gateway/SmartHotel.Gateway/ gateway/SmartHotel.Gateway/
RUN dotnet publish gateway/SmartHotel.Gateway/SmartHotel.Gateway.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5000
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 CMD curl --fail --silent http://localhost:5000/health || exit 1
ENTRYPOINT ["dotnet", "SmartHotel.Gateway.dll"]
