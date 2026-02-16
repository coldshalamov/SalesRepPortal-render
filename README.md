# Lead Management Portal

A comprehensive ASP.NET Core MVC application for managing sales leads with role-based access control, automatic lead expiry, and customer conversion tracking.

## Features

### 1. Secure Authentication & Authorization

- Custom user authentication with ASP.NET Identity
- Three role levels:
  - **Organization Admin**: Full access to all features
  - **Group Admin**: Manage leads within their sales group
  - **Sales Rep**: View and manage only their assigned leads

### 2. Lead Management

- Register new leads with complete contact information
- Duplicate prevention (same phone/email blocked for 90 days)
- Automatic 90-day expiry tracking
- Color-coded urgency indicators (Critical, High, Medium, Low)
- Lead status tracking (New, Contacted, Qualified, Proposal, Negotiation, Converted, Lost, Expired)

### 3. Lead Conversion Rules

- Leads must be converted within 90 days
- Automatic expiry after 90 days if not converted
- One-time 90-day extension available (Organization Admin only)

### 4. Dashboard

- Real-time statistics and metrics
- Pending vs Converted leads tracking
- Days remaining before expiry
- Critical and high-priority alerts
- Recent leads overview
- Conversion rate analytics

### 5. Customer Management

- Converted leads become customer records
- Track conversion metrics (days to convert)
- Search and filter capabilities
- Role-based visibility

### 6. Sales Group Management

- Create and manage sales groups
- Assign group administrators
- Track group performance

### 7. Lead Documents (S3-Backed)

- Upload documents to a lead (any authenticated role)
- Download documents for a lead (any authenticated role)
- Files stored securely in Amazon S3
- No delete capability by design (audit-friendly)

## Technology Stack

- **Framework**: ASP.NET Core 8.0 MVC
- **Database**: Entity Framework Core with SQL Server
- **Authentication**: ASP.NET Core Identity
- **UI**: Bootstrap 5, Bootstrap Icons
- **Pattern**: Code-First Approach with EF Core Migrations

## Prerequisites

- .NET 8.0 SDK or later
- SQL Server or SQL Server LocalDB
- Visual Studio 2022 or VS Code

## Getting Started

### 1. Clone or Navigate to Project Directory

```powershell
cd c:\Users\SivaSekharNalluri\Desktop\DirxNewSite
```

### 2. Restore NuGet Packages

```powershell
cd LeadManagementPortal
dotnet restore
```

### 3. Update Database Connection String (Optional)

Edit `appsettings.json` to change the connection string if needed:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=LeadManagementDB;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

### 4. Create Database Migration

```powershell
dotnet ef migrations add InitialCreate
```

### 5. Update Database

```powershell
dotnet ef database update
```

### 6. Run the Application

```powershell
dotnet run
```

The application will start at `https://localhost:5001` (or the port shown in console)

### 7. Configure AWS S3 for Lead Documents

1. In `LeadManagementPortal/appsettings.Development.json` (preferred) or environment variables, set the following under `AwsStorage`:

```json
{
  "AwsStorage": {
    "AccessKeyId": "YOUR_ACCESS_KEY",
    "SecretAccessKey": "YOUR_SECRET_KEY",
    "Region": "us-east-1",
    "BucketName": "your-bucket",
    "KeyPrefix": "",
    "UsePathStyle": false
  }
}
```

2. Ensure the S3 bucket exists and the IAM user/key has permissions: `s3:PutObject`, `s3:GetObject`, `s3:ListBucket` for that bucket.

3. Build and run; navigate to a Lead Details page to upload/download docs.

### 7. Default Login Credentials

**Organization Admin:**

- Email: `admin@leadportal.com`
- Password: `Admin@123`

## Database Schema

### Main Tables

1. **AspNetUsers** - User accounts with Identity
2. **AspNetRoles** - User roles
3. **SalesGroups** - Sales team groups
4. **Leads** - Lead records with tracking
5. **Customers** - Converted customer records

### Key Relationships

- Users belong to Sales Groups
- Leads are assigned to Users
- Leads belong to Sales Groups
- Customers reference original Leads

## Project Structure

```
LeadManagementPortal/
├── Controllers/          # MVC Controllers
│   ├── AccountController.cs
│   ├── DashboardController.cs
│   ├── LeadsController.cs
│   ├── CustomersController.cs
│   ├── SalesGroupsController.cs
│   └── UsersController.cs
├── Models/              # Domain Models
│   ├── ApplicationUser.cs
│   ├── Lead.cs
│   ├── Customer.cs
│   └── SalesGroup.cs
├── Data/                # Database Context
│   ├── ApplicationDbContext.cs
│   └── SeedData.cs
├── Services/            # Business Logic
│   ├── LeadService.cs
│   ├── CustomerService.cs
│   ├── SalesGroupService.cs
│   ├── DashboardService.cs
│   └── LeadExpiryBackgroundService.cs
├── Views/               # Razor Views
│   ├── Account/
│   ├── Dashboard/
│   ├── Leads/
│   ├── Customers/
│   ├── SalesGroups/
│   ├── Users/
│   └── Shared/
└── wwwroot/            # Static Files
    ├── css/
    └── js/
```

## Key Features Explained

### Lead Expiry System

- Background service runs hourly to check for expired leads
- Leads automatically marked as expired after 90 days
- Organization Admin can grant one-time 90-day extension
- Expired leads become available for re-registration

### Role-Based Access

**Organization Admin:**

- View all leads and customers
- Create sales groups
- Add/manage users
- Grant lead extensions
- Change lead status

**Group Admin:**

- View leads in their sales group
- Manage group members
- Cannot modify restricted fields

**Sales Rep:**

- View only assigned leads
- Register new leads
- Convert leads to customers
- Cannot change lead status

### Duplicate Prevention

- System checks phone and email before registration
- Blocks duplicate registration within 90 days
- Checks both active leads and recent customers
- Allows re-registration after lead expiry or 90 days post-conversion

## API Endpoints (Controllers)

### Dashboard

- `GET /Dashboard/Index` - View dashboard

### Leads

- `GET /Leads/Index` - List all accessible leads
- `GET /Leads/Details/{id}` - View lead details
- `GET /Leads/Create` - Show create form
- `POST /Leads/Create` - Create new lead
- `GET /Leads/Edit/{id}` - Show edit form
- `POST /Leads/Edit/{id}` - Update lead
- `POST /Leads/Convert/{id}` - Convert to customer
- `POST /Leads/GrantExtension/{id}` - Grant 90-day extension (Admin only)

### Customers

- `GET /Customers/Index` - List all customers
- `GET /Customers/Details/{id}` - View customer details

### Sales Groups (Admin only)

- `GET /SalesGroups/Index` - List all groups
- `GET /SalesGroups/Create` - Show create form
- `POST /SalesGroups/Create` - Create new group
- `GET /SalesGroups/Edit/{id}` - Show edit form
- `POST /SalesGroups/Edit/{id}` - Update group

### Users (Admin only)

- `GET /Users/Index` - List all users
- `GET /Users/Create` - Show create form
- `POST /Users/Create` - Create new user

## Configuration

### Background Services

The `LeadExpiryBackgroundService` runs every hour to automatically expire old leads. Configure the interval in `LeadExpiryBackgroundService.cs`:

```csharp
await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
```

### Password Policy

Configure password requirements in `Program.cs`:

```csharp
options.Password.RequireDigit = true;
options.Password.RequireLowercase = true;
options.Password.RequireUppercase = true;
options.Password.RequireNonAlphanumeric = false;
options.Password.RequiredLength = 6;
```

## Troubleshooting

### Database Connection Issues

If you encounter database connection errors:

1. Ensure SQL Server LocalDB is installed
2. Check the connection string in `appsettings.json`
3. Run migrations again: `dotnet ef database update`

### Migration Issues

To reset database:

```powershell
dotnet ef database drop
dotnet ef migrations remove
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Future Enhancements

- Email notifications for expiring leads
- Advanced reporting and analytics
- Lead assignment automation
- Activity logging and audit trail
- Import/Export functionality
- Mobile responsive improvements
- API endpoints for external integration

## License

This project is created for demonstration purposes.

## Support

For issues or questions, please refer to the project documentation or contact the development team.
