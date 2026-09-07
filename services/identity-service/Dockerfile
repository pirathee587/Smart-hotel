# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY SmartHotel.Identity/SmartHotel.Identity.API/SmartHotel.Identity.API.csproj SmartHotel.Identity/SmartHotel.Identity.API/
COPY SmartHotel.Identity/SmartHotel.Identity.Application/SmartHotel.Identity.Application.csproj SmartHotel.Identity/SmartHotel.Identity.Application/
COPY SmartHotel.Identity/SmartHotel.Identity.Domain/SmartHotel.Identity.Domain.csproj SmartHotel.Identity/SmartHotel.Identity.Domain/
COPY SmartHotel.Identity/SmartHotel.Identity.Grpc/SmartHotel.Identity.Grpc.csproj SmartHotel.Identity/SmartHotel.Identity.Grpc/
COPY SmartHotel.Identity/SmartHotel.Identity.Infrastructure/SmartHotel.Identity.Infrastructure.csproj SmartHotel.Identity/SmartHotel.Identity.Infrastructure/
COPY SmartHotel.Identity/SmartHotel.Identity.sln SmartHotel.Identity/

RUN dotnet restore SmartHotel.Identity/SmartHotel.Identity.API/SmartHotel.Identity.API.csproj

# Copy all source files
COPY SmartHotel.Identity/ SmartHotel.Identity/

# Build and publish
WORKDIR /src/SmartHotel.Identity/SmartHotel.Identity.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5001

ENTRYPOINT ["dotnet", "SmartHotel.Identity.API.dll"]
