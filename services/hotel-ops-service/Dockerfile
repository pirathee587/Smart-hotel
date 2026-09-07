# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY SmartHotel.HotelOps/SmartHotel.HotelOps.API/SmartHotel.HotelOps.API.csproj SmartHotel.HotelOps/SmartHotel.HotelOps.API/
COPY SmartHotel.HotelOps/SmartHotel.HotelOps.Application/SmartHotel.HotelOps.Application.csproj SmartHotel.HotelOps/SmartHotel.HotelOps.Application/
COPY SmartHotel.HotelOps/SmartHotel.HotelOps.Domain/SmartHotel.HotelOps.Domain.csproj SmartHotel.HotelOps/SmartHotel.HotelOps.Domain/
COPY SmartHotel.HotelOps/SmartHotel.HotelOps.Infrastructure/SmartHotel.HotelOps.Infrastructure.csproj SmartHotel.HotelOps/SmartHotel.HotelOps.Infrastructure/
COPY SmartHotel.HotelOps/SmartHotel.HotelOps.sln SmartHotel.HotelOps/

RUN dotnet restore SmartHotel.HotelOps/SmartHotel.HotelOps.API/SmartHotel.HotelOps.API.csproj

# Copy all source files
COPY SmartHotel.HotelOps/ SmartHotel.HotelOps/

# Build and publish
WORKDIR /src/SmartHotel.HotelOps/SmartHotel.HotelOps.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5002
EXPOSE 5012

ENTRYPOINT ["dotnet", "SmartHotel.HotelOps.API.dll"]
