# Lead Management Portal - Setup Script
# This script will help you set up the application

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Lead Management Portal - Setup Wizard" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check .NET SDK
Write-Host "Checking .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ .NET SDK installed: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "✗ .NET SDK not found. Please install .NET 8.0 SDK" -ForegroundColor Red
    Write-Host "Download from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Navigate to project directory (repo-relative)
$repoRoot = Split-Path -Parent $PSCommandPath
$projectPath = Join-Path $repoRoot "LeadManagementPortal"
Write-Host ""
Write-Host "Navigating to project directory..." -ForegroundColor Yellow
Set-Location $projectPath

# Restore packages
Write-Host ""
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Packages restored successfully" -ForegroundColor Green
} else {
    Write-Host "✗ Package restore failed" -ForegroundColor Red
    exit 1
}

# Build project
Write-Host ""
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Project built successfully" -ForegroundColor Green
} else {
    Write-Host "✗ Build failed" -ForegroundColor Red
    exit 1
}

# Database setup options
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Database Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Please choose your database option:" -ForegroundColor Yellow
Write-Host "1. SQL Server LocalDB (Recommended - requires SQL Server Express with LocalDB)"
Write-Host "2. SQL Server (Full installation)"
Write-Host "3. Docker SQL Server"
Write-Host "4. Skip database setup (I'll configure it manually)"
Write-Host ""

$choice = Read-Host "Enter your choice (1-4)"

switch ($choice) {
    "1" {
        Write-Host ""
        Write-Host "Using SQL Server LocalDB..." -ForegroundColor Yellow
        # Connection string already set in appsettings.json
        Write-Host "✓ Connection string configured for LocalDB" -ForegroundColor Green
    }
    "2" {
        Write-Host ""
        Write-Host "Using SQL Server..." -ForegroundColor Yellow
        $serverName = Read-Host "Enter SQL Server name (default: localhost)"
        if ([string]::IsNullOrWhiteSpace($serverName)) { $serverName = "localhost" }
        
        $useWinAuth = Read-Host "Use Windows Authentication? (y/n)"
        if ($useWinAuth -eq "y") {
            $connString = "Server=$serverName;Database=LeadManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
        } else {
            $userId = Read-Host "Enter User ID (default: sa)"
            if ([string]::IsNullOrWhiteSpace($userId)) { $userId = "sa" }
            $password = Read-Host "Enter Password" -AsSecureString
            $passwordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))
            $connString = "Server=$serverName;Database=LeadManagementDB;User Id=$userId;Password=$passwordPlain;MultipleActiveResultSets=true;TrustServerCertificate=True"
        }
        
        # Update appsettings.json
        $appsettings = Get-Content "appsettings.json" | ConvertFrom-Json
        $appsettings.ConnectionStrings.DefaultConnection = $connString
        $appsettings | ConvertTo-Json -Depth 10 | Set-Content "appsettings.json"
        Write-Host "✓ Connection string updated" -ForegroundColor Green
    }
    "3" {
        Write-Host ""
        Write-Host "Docker SQL Server setup..." -ForegroundColor Yellow
        Write-Host "Make sure Docker Desktop is installed and running" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Starting SQL Server container..." -ForegroundColor Yellow
        
        $password = "YourStrong@Passw0rd123"
        docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=$password" -p 1433:1433 --name lead-sql-server -d mcr.microsoft.com/mssql/server:2022-latest
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ SQL Server container started" -ForegroundColor Green
            Start-Sleep -Seconds 10
            
            $connString = "Server=localhost,1433;Database=LeadManagementDB;User Id=sa;Password=$password;MultipleActiveResultSets=true;TrustServerCertificate=True"
            $appsettings = Get-Content "appsettings.json" | ConvertFrom-Json
            $appsettings.ConnectionStrings.DefaultConnection = $connString
            $appsettings | ConvertTo-Json -Depth 10 | Set-Content "appsettings.json"
            Write-Host "✓ Connection string updated" -ForegroundColor Green
        } else {
            Write-Host "✗ Failed to start Docker container" -ForegroundColor Red
            Write-Host "Please ensure Docker Desktop is installed and running" -ForegroundColor Yellow
            exit 1
        }
    }
    "4" {
        Write-Host ""
        Write-Host "Skipping database setup..." -ForegroundColor Yellow
        Write-Host "Please configure your connection string in appsettings.json manually" -ForegroundColor Yellow
        Write-Host ""
        Read-Host "Press Enter when ready to continue"
    }
    default {
        Write-Host "Invalid choice. Exiting..." -ForegroundColor Red
        exit 1
    }
}

# Run migrations
if ($choice -ne "5") {
    Write-Host ""
    Write-Host "Running database migrations..." -ForegroundColor Yellow
    
    # Check if migration already exists
    # Ensure the EF CLI is available
    $efCli = dotnet tool list -g | Select-String "dotnet-ef"
    if (-not $efCli) {
        Write-Host "Installing dotnet-ef tool globally..." -ForegroundColor Yellow
        dotnet tool install -g dotnet-ef
    }

    # Use built-in 'dotnet ef' commands (works without global tool in SDK 8)
    if (Test-Path "Migrations") {
        Write-Host "Migrations folder exists, updating database..." -ForegroundColor Yellow
        dotnet ef database update
    } else {
        Write-Host "No migrations found. Creating initial migration..." -ForegroundColor Yellow
        dotnet ef migrations add InitialCreate
        Write-Host "Applying migration to database..." -ForegroundColor Yellow
        dotnet ef database update
    }
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Database created and migrations applied" -ForegroundColor Green
    } else {
        Write-Host "✗ Migration failed. Please check your database connection" -ForegroundColor Red
        Write-Host "Refer to DATABASE_SETUP.md for troubleshooting" -ForegroundColor Yellow
    }
}

# Final instructions
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "To run the application:" -ForegroundColor Yellow
Write-Host "  dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "Default login credentials:" -ForegroundColor Yellow
Write-Host "  Email: admin@leadportal.com" -ForegroundColor White
Write-Host "  Password: Admin@123" -ForegroundColor White
Write-Host ""
Write-Host "The application will be available at:" -ForegroundColor Yellow
Write-Host "  https://localhost:5001" -ForegroundColor White
Write-Host "  http://localhost:5000" -ForegroundColor White
Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
