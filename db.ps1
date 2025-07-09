# Database management script for Web Forum (PowerShell)
# Usage: .\db.ps1 [command]

param(
    [Parameter(Position=0)]
    [string]$Command
)

function Start-Database {
    Write-Host "Starting PostgreSQL database..." -ForegroundColor Green
    docker-compose up -d postgres
    Write-Host "Database started. Waiting for it to be ready..." -ForegroundColor Yellow
    
    # Wait for database to be ready
    $retries = 30
    do {
        $result = docker-compose exec postgres pg_isready -U postgres -d webforum 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Database is ready!" -ForegroundColor Green
            Update-Database
            return
        }
        Start-Sleep -Seconds 1
        $retries--
    } while ($retries -gt 0)
    
    Write-Host "Database may not be ready yet. Check with 'docker-compose logs postgres'" -ForegroundColor Yellow
}

function Update-Database {
    Write-Host "Applying EF Core migrations..." -ForegroundColor Green
    Push-Location "src\WebForum.Api"
    try {
        dotnet ef database update
        Write-Host "Migrations applied successfully!" -ForegroundColor Green
    }
    catch {
        Write-Host "Failed to apply migrations: $_" -ForegroundColor Red
        Write-Host "Make sure the .NET SDK is installed and the project builds successfully." -ForegroundColor Yellow
    }
    finally {
        Pop-Location
    }
}

function Stop-Database {
    Write-Host "Stopping database..." -ForegroundColor Yellow
    docker-compose down
}

function Restart-Database {
    Write-Host "Restarting database..." -ForegroundColor Yellow
    docker-compose restart postgres
}

function Reset-Database {
    Write-Host "WARNING: This will delete all data!" -ForegroundColor Red
    $confirmation = Read-Host "Are you sure? (y/N)"
    if ($confirmation -eq 'y' -or $confirmation -eq 'Y') {
        docker-compose down -v
        docker-compose up -d postgres
        Write-Host "Database reset complete! Waiting for database to be ready..." -ForegroundColor Green
        
        # Wait for database to be ready before applying migrations
        $retries = 30
        do {
            $result = docker-compose exec postgres pg_isready -U postgres -d webforum 2>$null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Database is ready! Applying migrations..." -ForegroundColor Green
                Update-Database
                return
            }
            Start-Sleep -Seconds 1
            $retries--
        } while ($retries -gt 0)
        
        Write-Host "Database may not be ready yet. Run '.\db.ps1 migrate' manually." -ForegroundColor Yellow
    } else {
        Write-Host "Reset cancelled." -ForegroundColor Yellow
    }
}

function Show-Logs {
    docker-compose logs -f postgres
}

function Connect-Database {
    docker-compose exec postgres psql -U postgres -d webforum
}

function Backup-Database {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupFile = "backup_$timestamp.sql"
    Write-Host "Creating backup: $backupFile" -ForegroundColor Green
    docker-compose exec postgres pg_dump -U postgres webforum | Out-File -FilePath $backupFile -Encoding UTF8
    Write-Host "Backup created: $backupFile" -ForegroundColor Green
}

function Show-Status {
    docker-compose ps
}

function Show-Help {
    Write-Host "Web Forum Database Management" -ForegroundColor Cyan
    Write-Host "Usage: .\db.ps1 [command]" -ForegroundColor White
    Write-Host ""
    Write-Host "Commands:" -ForegroundColor White
    Write-Host "  start   - Start the PostgreSQL database and apply migrations" -ForegroundColor Gray
    Write-Host "  stop    - Stop all services" -ForegroundColor Gray
    Write-Host "  restart - Restart the database" -ForegroundColor Gray
    Write-Host "  reset   - Reset database and apply migrations (WARNING: deletes all data)" -ForegroundColor Gray
    Write-Host "  migrate - Apply EF Core migrations to the database" -ForegroundColor Gray
    Write-Host "  logs    - Show database logs" -ForegroundColor Gray
    Write-Host "  connect - Connect to database via psql" -ForegroundColor Gray
    Write-Host "  backup  - Create a database backup" -ForegroundColor Gray
    Write-Host "  status  - Show service status" -ForegroundColor Gray
}

switch ($Command) {
    "start" { Start-Database }
    "stop" { Stop-Database }
    "restart" { Restart-Database }
    "reset" { Reset-Database }
    "migrate" { Update-Database }
    "logs" { Show-Logs }
    "connect" { Connect-Database }
    "backup" { Backup-Database }
    "status" { Show-Status }
    default { Show-Help }
}
