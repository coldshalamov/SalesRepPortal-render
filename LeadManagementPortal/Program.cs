using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddHttpContextAccessor();

// Configure Database
var dbProvider = builder.Configuration["DatabaseProvider"]?.Trim();
var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException("Missing ConnectionStrings:DefaultConnection configuration.");
}
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(defaultConnection);
    }
    else
    {
        options.UseSqlServer(defaultConnection);
    }
});

static void EnsureSqliteDataSourceDirectoryExists(string connectionString)
{
    var sqliteConnectionStringBuilder = new SqliteConnectionStringBuilder(connectionString);
    var dataSource = sqliteConnectionStringBuilder.DataSource?.Trim();
    if (string.IsNullOrWhiteSpace(dataSource) || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    if (dataSource.Contains("://", StringComparison.Ordinal))
    {
        return;
    }

    var fullPath = Path.GetFullPath(dataSource);
    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }
}

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure Cookie Settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// Register Services
builder.Services.AddScoped<ILeadService, LeadService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISalesGroupService, SalesGroupService>();
builder.Services.AddScoped<ISalesOrgService, SalesOrgService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IAddressService, AddressService>();
var azureConfigured =
    !string.IsNullOrWhiteSpace(builder.Configuration["AzureStorage:ConnectionString"]) ||
    (
        !string.IsNullOrWhiteSpace(builder.Configuration["AzureStorage:AccountName"]) &&
        !string.IsNullOrWhiteSpace(builder.Configuration["AzureStorage:AccountKey"]) &&
        !string.IsNullOrWhiteSpace(builder.Configuration["AzureStorage:ContainerName"])
    );
builder.Services.AddScoped<IFileStorageService>(sp =>
    azureConfigured
        ? ActivatorUtilities.CreateInstance<AzureBlobStorageService>(sp)
        : ActivatorUtilities.CreateInstance<LocalFileStorageService>(sp));
builder.Services.AddScoped<ILeadDocumentService, LeadDocumentService>();
builder.Services.AddScoped<ILeadAuditService, LeadAuditService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Options
builder.Services.Configure<SmartyStreetsOptions>(builder.Configuration.GetSection("SmartyStreets"));
builder.Services.Configure<AzureStorageOptions>(builder.Configuration.GetSection("AzureStorage"));
builder.Services.Configure<LocalStorageOptions>(builder.Configuration.GetSection("LocalStorage"));
builder.Services.AddHttpClient();

// Add Background Service for Lead Expiry
builder.Services.AddHostedService<LeadExpiryBackgroundService>();

// Forwarded headers (Render / reverse proxies)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        if (db.Database.IsSqlite())
        {
            EnsureSqliteDataSourceDirectoryExists(db.Database.GetDbConnection().ConnectionString);
            await db.Database.EnsureCreatedAsync();
        }
        else
        {
            await db.Database.MigrateAsync();
        }
        await SeedData.Initialize(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
        throw;
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
