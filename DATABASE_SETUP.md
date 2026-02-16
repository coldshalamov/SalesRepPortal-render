# Database Setup Guide

## Option 1: SQL Server LocalDB (Recommended for Development)

### Install SQL Server LocalDB

1. Download and install SQL Server Express with LocalDB:

   - Visit: https://www.microsoft.com/en-us/sql-server/sql-server-downloads
   - Choose "Express" edition
   - During installation, select "LocalDB" component

2. After installation, use the default connection string in `appsettings.json`:
   ```json
   "Server=(localdb)\\mssqllocaldb;Database=LeadManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true"
   ```

## Option 2: Full SQL Server Installation

If you have SQL Server installed (not LocalDB):

1. Update `appsettings.json` connection string to:

   ```json
   "Server=localhost;Database=LeadManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
   ```

2. Or with SQL authentication:
   ```json
   "Server=localhost;Database=LeadManagementDB;User Id=sa;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True"
   ```

## Option 3: Using Docker SQL Server

1. Install Docker Desktop for Windows

2. Run SQL Server in Docker:

   ```powershell
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" -p 1433:1433 --name sql-server -d mcr.microsoft.com/mssql/server:2022-latest
   ```

3. Update `appsettings.json`:
   ```json
   "Server=localhost,1433;Database=LeadManagementDB;User Id=sa;Password=YourStrong@Passw0rd;MultipleActiveResultSets=true;TrustServerCertificate=True"
   ```

## Running Migrations

After configuring your database connection:

```powershell
# Navigate to project folder
cd c:\Users\SivaSekharNalluri\Desktop\DirxNewSite\LeadManagementPortal

# Create migration (if not already created)
dotnet dotnet-ef migrations add InitialCreate

# Apply migration to database
dotnet dotnet-ef database update
```

## Verify Database Creation

You can verify the database was created by:

1. **Using SQL Server Management Studio (SSMS)**

   - Connect to your SQL Server instance
   - Look for "LeadManagementDB" database

2. **Using Azure Data Studio**

   - Connect to your SQL Server
   - Expand Databases to see LeadManagementDB

3. **Using Command Line**
   ```powershell
   sqlcmd -S (localdb)\mssqllocaldb -Q "SELECT name FROM sys.databases WHERE name = 'LeadManagementDB'"
   ```

## Troubleshooting

### "Cannot locate Local Database Runtime"

- Install SQL Server Express with LocalDB from the official Microsoft download page

### "Login failed for user"

- Ensure SQL Server is running
- Check your connection string credentials
- Verify SQL Server Authentication is enabled (for SQL auth)

### "A network-related or instance-specific error"

- Ensure SQL Server service is running
- Check firewall settings
- Verify server name in connection string

## Quick Start (Summary)

For development and production, this application is intended to run on SQL Server (LocalDB, full SQL Server, or Docker-hosted SQL Server). SQLite is no longer a supported option.
