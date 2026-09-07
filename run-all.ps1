<#
.SYNOPSIS
    SmartHotel Full-Stack Native Runner for Windows
.DESCRIPTION
    Launches all backend microservices, the API gateway, and the Next.js frontend
    directly on Windows using installed runtimes (.NET 8, Python 3.14, Node 22).
#>

[CmdletBinding()]
param (
    [switch]$Background,
    [switch]$Stop
)

$ErrorActionPreference = "Stop"
$WorkspaceRoot = "c:\Users\Piratheepan\Desktop\Project1"

if ($Stop) {
    Write-Host "[SmartHotel] Stopping all services on ports 3000, 5000, 5001, 5002, 5003, 5004, 5005..." -ForegroundColor Yellow
    $ports = @(3000, 5000, 5001, 5002, 5003, 5004, 5005, 5012, 5013)
    foreach ($port in $ports) {
        $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
        foreach ($conn in $conns) {
            $pidToKill = $conn.OwningProcess
            if ($pidToKill -and $pidToKill -ne 0) {
                Write-Host "Killing process $pidToKill on port $port" -ForegroundColor Gray
                Stop-Process -Id $pidToKill -Force -ErrorAction SilentlyContinue
            }
        }
    }
    Write-Host "[SmartHotel] All services stopped." -ForegroundColor Green
    return
}

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "          SmartHotel Distributed System - Native Runner          " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Identity Service (5001)
Write-Host "--> Starting Identity Service on http://localhost:5001..." -ForegroundColor Green
Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "`$host.ui.RawUI.WindowTitle='SmartHotel Identity Service (5001)'; Set-Location '$WorkspaceRoot'; dotnet run --project .\services\identity-service\SmartHotel.Identity\SmartHotel.Identity.API\SmartHotel.Identity.API.csproj --launch-profile http"

Start-Sleep -Seconds 3

# 2. Hotel Operations Service (5002 / 5012)
Write-Host "--> Starting Hotel Operations Service on http://localhost:5002 (gRPC 5012)..." -ForegroundColor Green
Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "`$host.ui.RawUI.WindowTitle='SmartHotel HotelOps Service (5002)'; Set-Location '$WorkspaceRoot'; dotnet run --project .\services\hotel-ops-service\SmartHotel.HotelOps\SmartHotel.HotelOps.API\SmartHotel.HotelOps.API.csproj --launch-profile http"

Start-Sleep -Seconds 2

# 3. Booking & Payments Service (5003 / 5013)
Write-Host "--> Starting Booking & Payments Service on http://localhost:5003 (gRPC 5013)..." -ForegroundColor Green
Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "`$host.ui.RawUI.WindowTitle='SmartHotel Booking Service (5003)'; Set-Location '$WorkspaceRoot'; dotnet run --project .\services\booking-payments-service\SmartHotel.Booking\SmartHotel.Booking.API\SmartHotel.Booking.API.csproj --launch-profile http"

Start-Sleep -Seconds 2

# 4. Notification Service (5004)
Write-Host "--> Starting Notification Service on http://localhost:5004..." -ForegroundColor Green
Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "`$host.ui.RawUI.WindowTitle='SmartHotel Notification Service (5004)'; Set-Location '$WorkspaceRoot'; dotnet run --project .\services\notification-service\SmartHotel.Notifications\SmartHotel.Notifications.API\SmartHotel.Notifications.API.csproj --launch-profile http"

Start-Sleep -Seconds 2

# 5. AI Concierge Service (5005)
Write-Host "--> Starting AI Concierge Service on http://localhost:5005..." -ForegroundColor Green
Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "`$host.ui.RawUI.WindowTitle='SmartHotel Concierge Service (5005)'; Set-Location '$WorkspaceRoot\services\ai-concierge-service\smarthotel-concierge'; `$env:PORT=5005; `$env:JWT_JWKS_URI='http://localhost:5001/.well-known/jwks.json'; `$env:BOOKING_SERVICE_URL='http://localhost:5003'; `$env:BOOKING_GRPC_HOST='localhost'; `$env:BOOKING_GRPC_PORT=5013; `$env:RABBITMQ_HOST='localhost'; python -m uvicorn app.main:app --host 0.0.0.0 --port 5005"

Start-Sleep -Seconds 2

# 6. YARP API Gateway (5000)
Write-Host "--> Starting SmartHotel Gateway on http://localhost:5000..." -ForegroundColor Green
Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "`$host.ui.RawUI.WindowTitle='SmartHotel API Gateway (5000)'; Set-Location '$WorkspaceRoot'; dotnet run --project .\gateway\SmartHotel.Gateway\SmartHotel.Gateway.csproj --launch-profile http"

Start-Sleep -Seconds 3

# 7. Frontend Next.js (3000)
Write-Host "--> Starting Next.js Frontend on http://localhost:3000..." -ForegroundColor Green
Start-Process -FilePath "powershell.exe" -ArgumentList "-NoExit", "-Command", "`$host.ui.RawUI.WindowTitle='SmartHotel Next.js Frontend (3000)'; Set-Location '$WorkspaceRoot\frontend'; `$env:PORT=3000; node .next\standalone\server.js"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "              All services have been dispatched!                 " -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "Frontend Portal:     http://localhost:3000" -ForegroundColor Yellow
Write-Host "API Gateway:         http://localhost:5000" -ForegroundColor Yellow
Write-Host "Identity API:        http://localhost:5001/swagger" -ForegroundColor Yellow
Write-Host "HotelOps API:        http://localhost:5002/swagger" -ForegroundColor Yellow
Write-Host "Booking API:         http://localhost:5003/swagger" -ForegroundColor Yellow
Write-Host "Notification API:    http://localhost:5004/swagger" -ForegroundColor Yellow
Write-Host "AI Concierge API:    http://localhost:5005/docs" -ForegroundColor Yellow
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "To stop all services later, run: .\run-all.ps1 -Stop" -ForegroundColor Gray
