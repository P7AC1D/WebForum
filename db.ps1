# Database management script for Web Forum (PowerShell)
# Usage: .\db.ps1 [command] [args]

param(
  [Parameter(Position = 0)]
  [string]$Command,
  [Parameter(Position = 1)]
  [string]$MigrationName = "InitialCreate"
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
    # Check if Migrations folder exists or if there are any migrations
    if (!(Test-Path "Migrations") -or !(Get-ChildItem "Migrations" -Filter "*.cs" -ErrorAction SilentlyContinue)) {
      Write-Host "No migrations found. Creating initial migration..." -ForegroundColor Yellow
      dotnet ef migrations add "InitialCreate"
      if ($LASTEXITCODE -ne 0) {
        throw "Failed to create initial migration"
      }
      Write-Host "Initial migration created successfully!" -ForegroundColor Green
    }
    
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
  Write-Host "WARNING: This will delete all data and reset migrations!" -ForegroundColor Red
  $confirmation = Read-Host "Are you sure? (y/N)"
  if ($confirmation -eq 'y' -or $confirmation -eq 'Y') {
    Write-Host "Stopping containers and removing volumes..." -ForegroundColor Yellow
    docker-compose down -v
    
    Write-Host "Clearing migrations directory..." -ForegroundColor Yellow
    $migrationsPath = "src\WebForum.Api\Migrations"
    if (Test-Path $migrationsPath) {
      Remove-Item -Recurse -Force $migrationsPath
      Write-Host "Migrations directory cleared." -ForegroundColor Green
    }
    
    Write-Host "Starting fresh database..." -ForegroundColor Yellow
    docker-compose up -d postgres
    Write-Host "Database reset complete! Waiting for database to be ready..." -ForegroundColor Green
        
    # Wait for database to be ready before applying migrations
    $retries = 30
    do {
      $result = docker-compose exec postgres pg_isready -U postgres -d webforum 2>$null
      if ($LASTEXITCODE -eq 0) {
        Write-Host "Database is ready! Creating and applying fresh migrations..." -ForegroundColor Green
        Update-Database
        return
      }
      Start-Sleep -Seconds 1
      $retries--
    } while ($retries -gt 0)
        
    Write-Host "Database may not be ready yet. Run '.\db.ps1 migrate' manually." -ForegroundColor Yellow
  }
  else {
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

function Seed-Database {
  param(
    [int]$Users = 10,
    [int]$Posts = 25,
    [int]$Comments = 50,
    [int]$Likes = 75,
    [switch]$Force
  )
  
  Write-Host "🌱 Seeding database with test data..." -ForegroundColor Green
  Write-Host "├── Users: $Users" -ForegroundColor Cyan
  Write-Host "├── Posts: $Posts" -ForegroundColor Cyan
  Write-Host "├── Comments: $Comments" -ForegroundColor Cyan
  Write-Host "└── Likes: $Likes" -ForegroundColor Cyan
  Write-Host ""
  
  Push-Location "src\WebForum.DataSeeder"
  try {
    $args = @("--users", $Users, "--posts", $Posts, "--comments", $Comments, "--likes", $Likes)
    if ($Force) {
      $args += "--force"
    }
    
    dotnet run -- @args
    
    if ($LASTEXITCODE -eq 0) {
      Write-Host ""
      Write-Host "✅ Database seeded successfully!" -ForegroundColor Green
      Write-Host ""
      Write-Host "🎯 Ready for assessment:" -ForegroundColor Yellow
      Write-Host "├── API: https://localhost:7094" -ForegroundColor White
      Write-Host "├── Scalar UI: https://localhost:7094/scalar/v1" -ForegroundColor White
      Write-Host "└── pgAdmin: http://localhost:5050" -ForegroundColor White
      Write-Host ""
      Write-Host "💡 Test credentials:" -ForegroundColor Yellow
      Write-Host "├── Admin: admin@webforum.com / password123" -ForegroundColor White
      Write-Host "├── Moderator: moderator@webforum.com / password123" -ForegroundColor White
      Write-Host "└── User: testuser@webforum.com / password123" -ForegroundColor White
    } else {
      Write-Host "❌ Database seeding failed!" -ForegroundColor Red
    }
  }
  catch {
    Write-Host "Failed to seed database: $_" -ForegroundColor Red
  }
  finally {
    Pop-Location
  }
}

function Show-Help {
  Write-Host "Web Forum Database Management" -ForegroundColor Cyan
  Write-Host "Usage: .\db.ps1 [command] [options]" -ForegroundColor White
  Write-Host ""
  Write-Host "Commands:" -ForegroundColor White
  Write-Host "  start          - Start the PostgreSQL database and apply migrations" -ForegroundColor Gray
  Write-Host "  stop           - Stop all services" -ForegroundColor Gray
  Write-Host "  restart        - Restart the database" -ForegroundColor Gray
  Write-Host "  reset          - Reset database and migrations (WARNING: deletes all data and migrations)" -ForegroundColor Gray
  Write-Host "  migrate        - Apply EF Core migrations to the database" -ForegroundColor Gray
  Write-Host "  seed           - Populate database with realistic test data for assessment" -ForegroundColor Gray
  Write-Host "  logs           - Show database logs" -ForegroundColor Gray
  Write-Host "  connect        - Connect to database via psql" -ForegroundColor Gray
  Write-Host "  backup         - Create a database backup" -ForegroundColor Gray
  Write-Host "  status         - Show service status" -ForegroundColor Gray
  Write-Host ""
  Write-Host "Seed Options:" -ForegroundColor White
  Write-Host "  -Users [count]    - Number of users to create (default: 10)" -ForegroundColor Gray
  Write-Host "  -Posts [count]    - Number of posts to create (default: 25)" -ForegroundColor Gray
  Write-Host "  -Comments [count] - Number of comments to create (default: 50)" -ForegroundColor Gray
  Write-Host "  -Likes [count]    - Number of likes to create (default: 75)" -ForegroundColor Gray
  Write-Host "  -Force            - Overwrite existing data" -ForegroundColor Gray
  Write-Host ""
  Write-Host "Examples:" -ForegroundColor White
  Write-Host "  .\db.ps1 seed                              - Seed with default amounts" -ForegroundColor Gray
  Write-Host "  .\db.ps1 seed -Users 20 -Posts 50         - Seed with custom amounts" -ForegroundColor Gray
  Write-Host "  .\db.ps1 seed -Force                       - Overwrite existing data" -ForegroundColor Gray
}

# Parse additional parameters for seed command
$SeedUsers = 10
$SeedPosts = 25
$SeedComments = 50
$SeedLikes = 75
$Force = $false

# Process named parameters
for ($i = 1; $i -lt $args.Count; $i++) {
  switch ($args[$i]) {
    "-Users" { 
      if (($i + 1) -lt $args.Count) { 
        $SeedUsers = [int]$args[++$i] 
      }
    }
    "-Posts" { 
      if (($i + 1) -lt $args.Count) { 
        $SeedPosts = [int]$args[++$i] 
      }
    }
    "-Comments" { 
      if (($i + 1) -lt $args.Count) { 
        $SeedComments = [int]$args[++$i] 
      }
    }
    "-Likes" { 
      if (($i + 1) -lt $args.Count) { 
        $SeedLikes = [int]$args[++$i] 
      }
    }
    "-Force" { 
      $Force = $true 
    }
  }
}

switch ($Command) {
  "start" { Start-Database }
  "stop" { Stop-Database }
  "restart" { Restart-Database }
  "reset" { Reset-Database }
  "migrate" { Update-Database }
  "seed" { Seed-Database -Users $SeedUsers -Posts $SeedPosts -Comments $SeedComments -Likes $SeedLikes -Force:$Force }
  "logs" { Show-Logs }
  "connect" { Connect-Database }
  "backup" { Backup-Database }
  "status" { Show-Status }
  default { Show-Help }
}
