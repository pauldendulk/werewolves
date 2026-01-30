#!/usr/bin/env pwsh

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Werewolves Game - Shutdown Script" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""

# Backend configuration
$backendPort = 5000
$backendProcessName = "dotnet"

# Frontend configuration
$frontendPort = 4201
$frontendProcessName = "node"

# Function to kill processes on a specific port
function Stop-ProcessOnPort {
    param (
        [int]$Port,
        [string]$ServiceName
    )
    
    Write-Host "[$ServiceName] Checking for processes on port $Port..." -ForegroundColor Yellow
    
    $connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    
    if ($connections) {
        foreach ($connection in $connections) {
            $processId = $connection.OwningProcess
            $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
            
            if ($process) {
                Write-Host "[$ServiceName] Stopping process: $($process.ProcessName) (PID: $processId)" -ForegroundColor Yellow
                Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
                Write-Host "[$ServiceName] Process stopped." -ForegroundColor Green
            }
        }
    } else {
        Write-Host "[$ServiceName] No process found on port $Port" -ForegroundColor Gray
    }
}

# Stop backend
Stop-ProcessOnPort -Port $backendPort -ServiceName "Backend"

Write-Host ""

# Stop frontend
Stop-ProcessOnPort -Port $frontendPort -ServiceName "Frontend"

# Also try to stop any remaining node/dotnet processes that might be related
Write-Host ""
Write-Host "[Cleanup] Checking for orphaned processes..." -ForegroundColor Yellow

# Stop any remaining dotnet processes running from the backend path
$dotnetProcesses = Get-Process -Name $backendProcessName -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -like "*werewolves*" -or $_.CommandLine -like "*WerewolvesAPI*"
}
if ($dotnetProcesses) {
    $dotnetProcesses | ForEach-Object {
        Write-Host "[Cleanup] Stopping dotnet process (PID: $($_.Id))" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
}

# Stop any remaining node processes running from the frontend path
$nodeProcesses = Get-Process -Name $frontendProcessName -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -like "*werewolves*" -or $_.CommandLine -like "*werewolves-app*"
}
if ($nodeProcesses) {
    $nodeProcesses | ForEach-Object {
        Write-Host "[Cleanup] Stopping node process (PID: $($_.Id))" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Shutdown Complete!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host ""
