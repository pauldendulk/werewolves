#!/usr/bin/env pwsh

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Werewolves Game - Startup Script" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Function to check if a port is in use
function Test-Port {
    param (
        [int]$Port
    )
    $connection = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    return $null -ne $connection
}

# Function to stop process on a specific port
function Stop-ProcessOnPort {
    param (
        [int]$Port,
        [string]$ServiceName
    )
    
    $connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    
    if ($connections) {
        Write-Host "[$ServiceName] Port $Port is in use - stopping existing process..." -ForegroundColor Yellow
        
        foreach ($connection in $connections) {
            $processId = $connection.OwningProcess
            $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
            
            if ($process) {
                Write-Host "[$ServiceName] Stopping process: $($process.ProcessName) (PID: $processId)" -ForegroundColor Yellow
                Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            }
        }
        
        # Wait for port to be released
        $maxWaitSeconds = 5
        $waited = 0
        while ((Test-Port -Port $Port) -and ($waited -lt $maxWaitSeconds)) {
            Start-Sleep -Milliseconds 500
            $waited += 0.5
        }
        
        if (Test-Port -Port $Port) {
            Write-Host "[$ServiceName] Warning: Port $Port is still in use after stopping process" -ForegroundColor Red
            return $false
        } else {
            Write-Host "[$ServiceName] Port $Port is now free" -ForegroundColor Green
            return $true
        }
    }
    return $true
}

# Backend configuration
$backendPort = 5000
$backendPath = "backend\WerewolvesAPI"
$backendUrl = "http://localhost:$backendPort"

# Frontend configuration
$frontendPort = 4201
$frontendPath = "frontend\werewolves-app"
$frontendUrl = "http://localhost:$frontendPort"

# Check and start backend
Write-Host "[Backend] Checking port $backendPort..." -ForegroundColor Yellow
if (Test-Port -Port $backendPort) {
    Stop-ProcessOnPort -Port $backendPort -ServiceName "Backend"
}

Write-Host "[Backend] Starting .NET API on $backendUrl" -ForegroundColor Cyan

Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$backendPath'; Write-Host 'Starting Backend API...' -ForegroundColor Cyan; dotnet run --urls='$backendUrl'"

Write-Host "[Backend] Backend starting in new window..." -ForegroundColor Green
Write-Host "[Backend] API will be available at: $backendUrl/api" -ForegroundColor Green
Write-Host "[Backend] Swagger UI: $backendUrl/swagger" -ForegroundColor Green

# Wait a bit for backend to start
Start-Sleep -Seconds 3

Write-Host ""

# Check and start frontend
Write-Host "[Frontend] Checking port $frontendPort..." -ForegroundColor Yellow
if (Test-Port -Port $frontendPort) {
    Stop-ProcessOnPort -Port $frontendPort -ServiceName "Frontend"
}

Write-Host "[Frontend] Starting Angular app on $frontendUrl" -ForegroundColor Cyan

Start-Process pwsh -ArgumentList "-NoExit", "-Command", "cd '$frontendPath'; Write-Host 'Starting Frontend App...' -ForegroundColor Cyan; npm start -- --port $frontendPort"

Write-Host "[Frontend] Frontend starting in new window..." -ForegroundColor Green
Write-Host "[Frontend] App will be available at: $frontendUrl" -ForegroundColor Green

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Startup Complete!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Services:" -ForegroundColor White
Write-Host "  Frontend:  $frontendUrl" -ForegroundColor White
Write-Host "  Backend:   $backendUrl/api" -ForegroundColor White
Write-Host "  Swagger:   $backendUrl/swagger" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C in each window to stop the services" -ForegroundColor Gray
Write-Host ""
