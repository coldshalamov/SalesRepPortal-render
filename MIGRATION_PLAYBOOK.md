# Migration Playbook (Sandbox -> Work Repo)

This file defines the process for porting changes from `SalesRepPortal-render`
into the work repo safely.

Preferred approach: a stack of smaller, feature-based PRs.
Alternative: a single integration PR (kept below for when leadership insists).

---

## How This Works (Plain English)

This sandbox repo (`SalesRepPortal-render`) is a copy of the production repo where
we built new features. Now we need to move those features back into the production
repo without breaking anything. Here's the core idea:

1. **We never touch the production database or server directly.** Everything goes
   through Pull Requests in the work repo, which get reviewed and tested before
   anyone clicks "merge."
2. **Schema changes (new database tables) are additive only.** We're adding two
   new tables (`Notifications` and `LeadFollowUpTasks`). We are NOT modifying or
   deleting any existing tables, columns, or data. Existing data stays untouched.
3. **We port features one at a time**, in a specific order. Each PR is small enough
   to review and roll back independently.
4. **We test each PR against a staging copy of the production database** before
   merging. If the migration fails on staging, we fix it before touching production.
5. **We back up the production database before every schema PR** so we can restore
   if something goes wrong (it shouldn't, but the safety net is non-negotiable).

**What can NOT go wrong if you follow this playbook:**
- Data loss - impossible. We only run `CREATE TABLE` (additive). No `DROP`, no
  `ALTER COLUMN`, no `DELETE FROM`. Existing tables and rows are untouched.
- App crashes - caught in staging first. Each PR must build and pass tests before
  merge.
- Partial deploys - each PR is self-contained. If PR #3 fails, PRs #1 and #2
  still work fine on their own.

---

## Pre-Migration Setup (Do This Once, Before Any PRs)

### 1. Back Up the Production Database

Before merging ANY schema PR, create a full backup of the production SQL Server
database. This is your safety net.

```powershell
# Option A: Azure Portal (easiest)
# Go to Azure Portal > SQL Database > your-database > Backups > Create manual backup
# OR use Azure CLI:
az sql db export `
  --admin-user <admin> `
  --admin-password <password> `
  --auth-type SQL `
  --name <database-name> `
  --resource-group <resource-group> `
  --server <server-name> `
  --storage-key <storage-key> `
  --storage-key-type StorageAccessKey `
  --storage-uri "https://<storage-account>.blob.core.windows.net/backups/pre-migration-$(Get-Date -Format yyyyMMdd).bacpac"

# Option B: SQL Server Management Studio (SSMS)
# Right-click database > Tasks > Export Data-tier Application > Save .bacpac to disk

# Option C: sqlcmd / PowerShell (if you have direct access)
# Use BACKUP DATABASE ... TO DISK = '...' WITH COPY_ONLY
```

**Save the backup file name and timestamp. You'll need it if rollback is required.**

### 2. Create a Staging Database Clone

You need a staging database that mirrors production to test migrations against
BEFORE merging PRs. This is how you verify that the migration won't break prod.

```powershell
# Option A: Azure Portal - copy the production database
# Go to Azure Portal > SQL Database > your-database > Copy
# Name it: <database-name>-staging
# This creates an exact copy including all data.

# Option B: Restore from the backup you just made
# Import the .bacpac from step 1 into a new database named <database-name>-staging

# Option C: LocalDB (quick local test - no production data, but validates SQL)
# Install SQL Server Express LocalDB if needed, then update your local
# appsettings.Development.json:
#   "ConnectionStrings": {
#     "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=LeadManagement_Staging;Trusted_Connection=True"
#   }
# Then run: dotnet ef database update --project LeadManagementPortal/LeadManagementPortal.csproj
```

**Which option should you pick?**
- If you have Azure access and want to test with real data: Option A (best)
- If you just want to verify the migration SQL is syntactically correct: Option C
- Both is ideal - Option C first (fast, local), then Option A (realistic, final)

### 3. Prepare Your Work Repo Clone

```powershell
# Clone the work repo fresh (do NOT work inside this sandbox repo)
git clone <work-repo-url> D:\GitHub\SalesRepPortal-work
cd D:\GitHub\SalesRepPortal-work
git checkout main
git pull origin main

# Verify it builds clean BEFORE making any changes
dotnet restore
dotnet build
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
```

If the work repo doesn't build clean before you start, fix that first - do not
proceed with porting until the baseline is green.

### 4. Determine How Migrations Are Applied in Production

Check whether the work repo applies migrations automatically on startup or manually:

```powershell
# In the work repo, search Program.cs for automatic migration
Select-String -Path LeadManagementPortal/Program.cs -Pattern "Migrate|EnsureCreated" -CaseSensitive:$false
```

- **If you find `MigrateAsync()` or `Migrate()`:** Migrations run automatically
  when the app starts. Merging a PR with a migration -> deploy -> app restarts ->
  migration applies. No manual step needed.
- **If you find nothing:** Migrations must be applied manually via
  `dotnet ef database update` before or during deployment.

**Write down which strategy your work repo uses. It affects the deploy sequence.**

---

## Environment Variables Checklist

New features require these environment variables (or appsettings entries) on the
production server. **Set these BEFORE deploying the code that uses them.**

| Variable / Config Key | Required By | Default | Notes |
|---|---|---|---|
| `AzureStorage:ConnectionString` | Feature 10 (file storage) | (existing) | Already set if Azure blob uploads work today |
| `AzureStorage:AccountName` | Feature 10 | (existing) | Alternative to ConnectionString |
| `AzureStorage:AccountKey` | Feature 10 | (existing) | Alternative to ConnectionString |
| `AzureStorage:ContainerName` | Feature 10 | (existing) | Alternative to ConnectionString |
| `LocalStorage:RootPath` | Feature 10 (fallback only) | `""` | Only needed if Azure is NOT configured |
| `LocalStorage:BaseUrlPath` | Feature 10 (fallback only) | `"/files"` | Only needed if Azure is NOT configured |

**If Azure blob storage is already configured in production** (which it almost
certainly is), you do NOT need to set any new environment variables. The local
storage fallback only activates when Azure keys are missing.

No other new features require new environment variables. Notifications, search,
pipeline, CSV export, and commissions all use the existing database connection
and identity system - no new secrets or config keys needed.

---

## Deploy Sequence (What Happens When You Merge a PR)

### For Schema PRs (PR #1: DB Prerequisites)

This is the highest-risk PR and requires the most care.

**If migrations run automatically on app startup:**
1. Back up production database (see step 1 above)
2. Test migration on staging clone first - run the app against staging and confirm
   the new tables appear
3. Merge the PR -> CI/CD deploys -> app restarts -> migration runs automatically
4. Monitor logs for 15 minutes (see Post-Deploy Monitoring below)
5. Verify new tables exist: `SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME IN ('Notifications', 'LeadFollowUpTasks')`

**If migrations run manually:**
1. Back up production database
2. Test migration on staging clone
3. Merge the PR -> CI/CD deploys new code (migration is in the code but not applied yet)
4. Apply migration manually:
   ```powershell
   dotnet ef database update `
     --project LeadManagementPortal/LeadManagementPortal.csproj `
     --startup-project LeadManagementPortal/LeadManagementPortal.csproj `
     --connection "<production-connection-string>"
   ```
5. Restart the app (so it picks up the new schema)
6. Monitor logs for 15 minutes

**Downtime:** A few seconds during app restart - this is normal for any deploy and
is not specific to migrations. Users might see a brief loading delay; no data is
affected.

### For Non-Schema PRs (PRs #2-8)

These PRs only add code (controllers, views, JS/CSS, tests). No database changes.

1. Merge the PR
2. CI/CD deploys -> app restarts
3. Verify the feature works (smoke test items for that PR)
4. No database backup needed - nothing changed in the DB

### Wait Between PRs

Deploy one PR at a time. Wait at least **15 minutes** between PR deploys and
verify each one works before moving to the next. If something breaks, you want
to know exactly which PR caused it.

---

## Emergency Rollback Runbook

### If a Schema Migration Fails on Production

**Symptoms:** App won't start, logs show `SqlException` or migration error.

**Step 1: Don't panic.** On SQL Server, EF Core migrations are transactional - if
the migration fails partway through, it rolls back automatically. Your data is safe.

**Step 2: Revert the code deploy.**
```powershell
# In the work repo
git revert <merge-commit-sha>
git push origin main
# Wait for CI/CD to redeploy the reverted code
```

**Step 3: If the migration DID apply but the app is broken:**
```powershell
# Find the name of the migration BEFORE the broken one
dotnet ef migrations list `
  --project LeadManagementPortal/LeadManagementPortal.csproj `
  --startup-project LeadManagementPortal/LeadManagementPortal.csproj

# Roll back to the previous migration
dotnet ef database update <PreviousMigrationName> `
  --project LeadManagementPortal/LeadManagementPortal.csproj `
  --startup-project LeadManagementPortal/LeadManagementPortal.csproj `
  --connection "<production-connection-string>"

# Then revert the code (Step 2 above)
```

**Step 4: Nuclear option - restore from backup.**
Only if Steps 2-3 don't work (extremely unlikely with additive-only migrations):
```powershell
# Azure Portal: SQL Database > Restore > Point-in-time restore
# Pick a timestamp BEFORE the migration was applied
# OR import the .bacpac you saved in Pre-Migration Setup step 1
```

### If a Non-Schema PR Breaks Something

Much simpler - revert the merge commit:
```powershell
git revert <merge-commit-sha>
git push origin main
# CI/CD redeploys the reverted code. No database changes to worry about.
```

### Quick Reference: How To Find the Merge Commit SHA

```powershell
# See recent merges on main
git log --oneline --merges -10
# The SHA is the first column, e.g. "a1b2c3d Merge pull request #42 ..."
```

---

## Post-Deploy Monitoring

After deploying each PR to production, monitor for **15 minutes minimum** before
deploying the next PR.

### What To Watch

| Check | How | Healthy |
|---|---|---|
| App starts | Azure Portal > App Service > Overview > Status | "Running" |
| No startup errors | Azure Portal > App Service > Log stream (or Diagnose > Application Logs) | No `SqlException`, no `InvalidOperationException`, no `DependencyResolutionException` |
| HTTP 200s | Hit the login page in a browser | Page loads without 500 error |
| DB connectivity | Log in with a test account | Dashboard loads with real data |
| New feature works | Run the smoke test items for that PR | All pass |

### If Monitoring Reveals Issues

1. **App crashes on startup** -> Likely a missing DI registration or missing table.
   Check logs. Rollback immediately (see runbook above).
2. **App starts but a page 500s** -> Likely a missing table, missing nav property,
   or null reference. Check which URL fails. If it's a new feature URL, it won't
   affect existing users. Fix-forward if possible, rollback if it affects core flows.
3. **UI looks wrong** -> CSS regression. Not urgent. Fix-forward with a follow-up PR.
4. **Existing feature broke** -> Rollback immediately. Something in the new code
   changed existing behavior.

---

## Latest Compatibility Audit (2026-02-25)

- Sandbox: `D:\GitHub\SalesRepPortal-render`
- Work snapshot zip: `D:\GitHub\SalesRepPortal-render\SalesRepPortal-main.zip`
  - Last write: 2026-02-24 22:41:36
  - SHA256: `32DFBB55EA654DE999E4B2CC1A0E0B921291D778559377C0B60169F7375E9DFC`
- Sandbox HEAD at time of audit: `2180e7ea5a98d0e46fadcda4a798c1a30e9ab1fb`
- Portable-file diff (using the same include/exclude rules as `scripts/ci/portability-target-dry-run.ps1`):
  - Total portable files considered: **150**
  - Present only in sandbox: **39**
  - Same content: **62**
  - Different content: **49**

Reproduce:
```powershell
./scripts/ci/portability-audit.ps1 -TargetRepoZipPath .\SalesRepPortal-main.zip
```

Notes:
- "Same/different" is computed after normalizing line endings for text files
  (to avoid CRLF-only churn when comparing to the zip snapshot).
- The sandbox is still a superset; avoid bulk copying and slice PRs narrowly.

---

## Portability Risk Score

| Approach | Risk |
|---|---|
| Bulk "copy everything changed" | **9/10 - Do not do this** |
| Single integration PR using the guided sequence below | **3/10 - Acceptable** |
| Narrow feature-by-feature PRs | **2/10 - Lowest risk** |

---

## What Was Added (Feature Summary)

See `PORTING_LOG.md` for per-feature detail and exact file lists. Summary:

| # | Feature | Schema change? | New package? | Notes |
|---|---|---|---|---|
| 1 | Notification system (bell + API) | YES - `Notifications` table | No | |
| 2 | Lead follow-up task system (pipeline board) | YES - `LeadFollowUpTasks` table | No | |
| 3 | Lead CSV export | No | YES - CsvHelper 33.0.1 | |
| 4 | Global navbar search API + typeahead | No | No | |
| 5 | Customer edit (full CRUD) | No | No | |
| 6 | Commissions dashboard surface | No | No | |
| 7 | Login redesign + transition animation | No | No | |
| 8 | Dashboard redesign (stat cards, activity feed) | No | No | |
| 9 | Layout/nav overhaul (logo, search, bell) | No | No | |
| 10 | Local file storage fallback + protected download URLs | No | No | |
| 11 | Expanded .NET test suite (11+ classes) | No | No | |
| 12 | Playwright browser test suite | No | No | |
| 13 | GitHub Actions CI workflows | No | No | Approval required |

---

## Never Port These

- Render-only deploy artifacts:
  - `render.yaml`, `Dockerfile`, `.render/`, `RENDER.md`
- Render-only runtime behavior:
  - The `DatabaseProvider=Sqlite` configuration + `EnsureCreatedAsync()` startup path in `LeadManagementPortal/Program.cs`
    (Render-only unless the work repo explicitly wants SQLite support)
- Demo-only seeding:
  - Demo user/demo data seeding paths in `LeadManagementPortal/Data/SeedData.cs` (only enable via env flags)
- Environment-specific configuration files:
  - Do not port sandbox `LeadManagementPortal/appsettings.json` or `LeadManagementPortal/appsettings.Development.json`
    into the work repo. Keep configuration in the work repo as-is and move secrets to environment variables
    (the zip snapshot currently contains secrets in `LeadManagementPortal/appsettings.json` - scrub those first).
- Agent scaffolding docs (optional, usually skip):
  - Per-folder `AGENTS.md`, `CLAUDE.md`, `GEMINI.md`
- GitHub Actions workflows:
  - Do not port CI workflows without explicit approval. Private-repo CI can be policy-sensitive
    (cost, compliance, minutes, secrets). If approved, port only the workflows you want and keep them minimal.

---

## Current High-Risk Items

### Risk 1 - EF Schema Mismatch (BLOCKING)

Two new tables exist in sandbox that do not exist in the work repo:

- `Notifications` - required by Features 1, 9 (notification bell in layout)
- `LeadFollowUpTasks` - required by Features 2, 8 (pipeline board, overdue count)

Porting any code that references these tables without the migrations will cause a
runtime crash on first request that touches them.

**Mitigation:** Regenerate both migrations in the work repo and validate against a
SQL Server staging clone before any other code lands.

### Risk 2 - Notification Service Coupling

`LeadsController` injects `INotificationService` and calls `NotifyUserAsync` on
lead status changes. Porting lead pipeline code (Feature 2) without Feature 1 will
cause a DI / compile failure.

**Mitigation:** Always port Feature 1 (notification stack) before Feature 2.

### Risk 3 - CsvHelper Package Missing

The `Export()` action on `LeadsController` uses `CsvHelper`. If the code is ported
but the NuGet package is not added to `.csproj`, the project will not build.

**Mitigation:** Add `CsvHelper 33.0.1` to `.csproj` in the same commit as the
export action.

### Risk 4 - Program.cs Startup Drift

Sandbox `Program.cs` has significant additions:
- `AddHttpContextAccessor()` (needed by `LocalFileStorageService`)
- Conditional `IFileStorageService` registration (Azure vs local)
- `INotificationService` registration
- `LocalStorageOptions` configuration
- `ForwardedHeadersOptions` + `UseForwardedHeaders()` (reverse proxy support)
- `EnsureCreatedAsync()` SQLite branch (Render-only - do not port)
- Fail-fast re-throw on seed error (evaluate before porting)

**Mitigation:** Port `Program.cs` changes surgically, line by line, not as a bulk
replacement.

### Risk 5 - site.css and _Layout.cshtml Surface Area

Both files have extensive changes. Bulk-replacing them risks losing untracked
prod-only customizations and introducing style regressions.

**Mitigation:** Use `git diff` to review each changed section; port selectively.

---

## Recommended Approach: Feature-Based PR Stack

Port the sandbox work as multiple smaller PRs. This reduces merge conflicts and
makes schema changes reviewable.

Suggested stack (adjust to match the work repo's current mainline):

1. **PR: DB prerequisites**
   - Regenerate SQL Server migrations in the work repo for:
     - `Notifications`
     - `LeadFollowUpTasks`
   - Port the corresponding model/DbContext changes (no UI yet).
2. **PR: Notifications**
   - API + service + UI bell dropdown + minimal tests.
3. **PR: Lead pipeline follow-ups**
   - Follow-up task CRUD + pipeline UI + controller endpoints + tests.
4. **PR: Storage fallback + protected downloads**
   - `LocalFileStorageService`, `FilesController`, `LocalStorageOptions`, and the
     DI selection changes in `Program.cs`.
5. **PR: Search + customer edit**
   - Navbar search/typeahead + `CustomerEditViewModel` + customer edit view flow.
6. **PR: Commissions surface**
   - Commissions controller/viewmodel/view + tests.
7. **PR: Quality gates**
   - `scripts/ci/*` guardrails and additional `LeadManagementPortal.Tests/*`.
8. **Optional PRs (only if desired/approved)**
   - Playwright `tests/browser/*` (and any CI workflows to run it).
   - Render deploy artifacts (`render.yaml`, `Dockerfile`, `.render/`, `RENDER.md`).
   - UI-only polish (large diffs in `site.css` and `_Layout.cshtml`).

### Per-PR Hard Gates (Feature-Based Stack)

Before opening EACH PR, check:

- [ ] `dotnet build` exits 0 on the feature branch
- [ ] `dotnet test` exits 0 (all existing + new tests pass)
- [ ] No Render-only files in the PR diff
- [ ] PR description lists exactly which sandbox feature(s) it covers

**Additional gates for PR #1 (DB Prerequisites) only:**

- [ ] Production database backed up (see Pre-Migration Setup)
- [ ] Migration tested on staging clone - apply + rollback both succeed
- [ ] Generated migration SQL reviewed - contains only `CREATE TABLE`/`ADD COLUMN`/`CREATE INDEX`
- [ ] No `DROP TABLE`, `DROP COLUMN`, or `ALTER COLUMN` statements present

**After EACH PR is merged and deployed:**

- [ ] Monitor for 15 minutes (see Post-Deploy Monitoring)
- [ ] Smoke test the specific features from that PR
- [ ] Confirm existing features still work (login, leads list, customer list)
- [ ] Only then proceed to the next PR

---

## Integration Approach: Single Clean PR

This is the recommended path when leadership wants one PR in the work repo.

### Step 0 - Set Up Integration Branch in Work Repo

```powershell
# In the work repo
git checkout main
git pull
git checkout -b integration/feature-parity-20260225
```

### Step A - Schema Prerequisites

**Goal:** Both new tables exist and migrate cleanly on SQL Server.

**Files to create/modify in work repo:**

```
# New model files (copy from sandbox)
LeadManagementPortal/Models/Notification.cs
LeadManagementPortal/Models/LeadFollowUpTask.cs

# Modified model file (add navigation property only)
LeadManagementPortal/Models/Lead.cs
  - Add: public virtual ICollection<LeadFollowUpTask> FollowUpTasks { get; set; } = new();

# Modified DbContext (add DbSets + OnModelCreating config)
LeadManagementPortal/Data/ApplicationDbContext.cs
  - Add: DbSet<Notification> Notifications { get; set; }
  - Add: DbSet<LeadFollowUpTask> LeadFollowUpTasks { get; set; }
  - Add OnModelCreating config for both (copy from sandbox DbContext)
```

**Generate migrations (do NOT copy from sandbox):**

```powershell
# In work repo (PowerShell)
dotnet tool restore

# Ensure EF runs against SQL Server (not SQLite).
# If the work repo does not use DatabaseProvider switching, you can omit this.
$env:DatabaseProvider = "SqlServer"
$env:ConnectionStrings__DefaultConnection = "<your-sqlserver-connection-string>"

dotnet ef migrations add AddNotifications `
  --project LeadManagementPortal/LeadManagementPortal.csproj `
  --startup-project LeadManagementPortal/LeadManagementPortal.csproj `
  --context ApplicationDbContext

dotnet ef migrations add AddLeadFollowUpTasks `
  --project LeadManagementPortal/LeadManagementPortal.csproj `
  --startup-project LeadManagementPortal/LeadManagementPortal.csproj `
  --context ApplicationDbContext
```

Optional (recommended): generate and review SQL before applying to any shared DB:

```powershell
dotnet ef migrations script `
  --project LeadManagementPortal/LeadManagementPortal.csproj `
  --startup-project LeadManagementPortal/LeadManagementPortal.csproj `
  --context ApplicationDbContext `
  --idempotent `
  --output ./.tmp/migrations-idempotent.sql
```

**Validate:**

```powershell
dotnet restore
dotnet build
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
```

Run the generated migrations against a SQL Server staging clone and confirm:
- `UPDATE` applies cleanly
- `DOWN` (rollback) removes tables on a disposable DB (validate rollback in staging; do not rely on down-migrations as the production rollback plan)

**Commit:**
```
feat(db): add Notifications and LeadFollowUpTasks schema
```

---

### Step B - Service/Model Contracts

**Goal:** All service interfaces and implementations updated; no behavior-breaking
changes to existing methods.

**Files to modify/add in work repo:**

```
# Service interfaces (add new methods only - do not remove existing signatures)
LeadManagementPortal/Services/ILeadService.cs
  + SearchTopAsync(string term, string userId, string userRole, int maxResults)
  + GetFollowUpsForLeadsAsync(IEnumerable<string> leadIds, string userId, string userRole)
  + GetFollowUpsForLeadAsync(string leadId, string userId, string userRole)
  + AddFollowUpAsync(string leadId, string userId, string userRole, string type, string description, DateTime? dueDate)
  + CompleteFollowUpAsync(string leadId, int followUpId, string userId, string userRole)
  + DeleteFollowUpsAsync(string leadId, IEnumerable<int> followUpIds, string userId, string userRole)
  + GetOverdueFollowUpCountAsync(string userId, string userRole)

LeadManagementPortal/Services/ICustomerService.cs
  + GetAccessibleByIdAsync(string id, string userId, string userRole)
  + UpdateAsync(Customer customer)
  + SearchTopAsync(string term, string userId, string userRole, int maxResults)

# Service implementations
LeadManagementPortal/Services/LeadService.cs
  (add implementations for all above)

LeadManagementPortal/Services/CustomerService.cs
  (add implementations for all above)

# New service files
LeadManagementPortal/Services/INotificationService.cs     [copy from sandbox]
LeadManagementPortal/Services/NotificationService.cs      [copy from sandbox]

# New optional fallback files (dev convenience)
LeadManagementPortal/Models/LocalStorageOptions.cs        [copy from sandbox]
LeadManagementPortal/Services/LocalFileStorageService.cs  [copy from sandbox]

# New viewmodels
LeadManagementPortal/Models/ViewModels/CommissionDashboardViewModel.cs  [copy from sandbox]
LeadManagementPortal/Models/ViewModels/CustomerEditViewModel.cs         [copy from sandbox]
```

**Validate:**

```powershell
dotnet build
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
```

**Commit:**
```
feat(services): add notification service, follow-up task service methods, customer update/search
```

---

### Step C - Program.cs + Package Updates

**Goal:** New services are registered; CsvHelper package added; local storage
fallback wired. SQLite-only code stays out.

**Changes to make in work repo:**

```csharp
// Program.cs - add these registrations (do NOT add SQLite/EnsureCreated logic)

builder.Services.AddHttpContextAccessor();   // needed by LocalFileStorageService

// Replace hardcoded AzureBlobStorageService registration with conditional:
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

// Add notification service
builder.Services.AddScoped<INotificationService, NotificationService>();

// Add LocalStorage options (used only when Azure is not configured)
builder.Services.Configure<LocalStorageOptions>(builder.Configuration.GetSection("LocalStorage"));

// ForwardedHeaders (useful if work repo is ever behind a reverse proxy)
// -- include this if the work repo's hosting uses a proxy, skip if not applicable
builder.Services.Configure<ForwardedHeadersOptions>(options => { ... });
```

**Add to `.csproj`:**
```xml
<PackageReference Include="CsvHelper" Version="33.0.1" />
```

**Add to `appsettings.Development.json` only:**
```json
"LocalStorage": {
  "RootPath": "",
  "BaseUrlPath": "/files"
}
```

**Add controller route (only if porting `FilesController`):**
```csharp
// app.MapControllerRoute already covers /files/{token} via conventional routing
// No extra route registration needed
```

**Validate:**

```powershell
dotnet restore
dotnet build
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
```

**Commit:**
```
feat(startup): register notification service, conditional file storage, CsvHelper package
```

---

### Step D - Controllers

**Goal:** All new API endpoints and the commissions/files controllers added;
existing controllers have new actions only (no removal of existing actions).

**Files to add/modify in work repo:**

```
# New controllers (copy from sandbox)
LeadManagementPortal/Controllers/NotificationsApiController.cs
LeadManagementPortal/Controllers/SearchController.cs
LeadManagementPortal/Controllers/CommissionsController.cs
LeadManagementPortal/Controllers/FilesController.cs          (only if Step C includes LocalFileStorageService)

# Modified controllers (add new actions to existing files)
LeadManagementPortal/Controllers/LeadsController.cs
  + UpdateStatus([FromBody] LeadPipelineStatusUpdateRequest)  -- pipeline status API
  + AddFollowUp([FromBody] LeadFollowUpCreateRequest)
  + CompleteFollowUp([FromBody] LeadFollowUpCompleteRequest)
  + DeleteFollowUps([FromBody] LeadFollowUpDeleteRequest)
  + Export()                                                   -- CSV download
  + LeadExportRow inner class
  + ViewBag.OverdueFollowUpCount in Index()
  (Also: constructor gains INotificationService injection)

LeadManagementPortal/Controllers/CustomersController.cs
  + Edit(GET) / Edit(POST)   -- customer editing with org-boundary validation

LeadManagementPortal/Controllers/AccountController.cs
  + POST login now redirects to LoginTransition instead of Dashboard/Index
```

**Review notes:**
- `LeadsController` is the largest change. Diff line-by-line; do not blindly replace.
- Confirm `INotificationService` is injected via constructor (DI wired in Step C).

**Validate:**

```powershell
dotnet build
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
```

**Commit:**
```
feat(controllers): add pipeline status/follow-up endpoints, notification API, search API, customer edit, commissions scaffold
```

---

### Step E - Views and Frontend Assets

**Goal:** All UI changes land in one commit group; reviewers can see the full
visual scope.

**Files to add/modify in work repo:**

```
# New views (copy from sandbox)
LeadManagementPortal/Views/Account/LoginTransition.cshtml
LeadManagementPortal/Views/Commissions/Index.cshtml
LeadManagementPortal/Views/Customers/Edit.cshtml

# Modified views (careful diff/merge - do not bulk replace)
LeadManagementPortal/Views/Account/Login.cshtml            -- full redesign
LeadManagementPortal/Views/Dashboard/Index.cshtml          -- stat cards, activity feed
LeadManagementPortal/Views/Leads/Index.cshtml              -- Kanban board, follow-up sidebar, export button
LeadManagementPortal/Views/Shared/_Layout.cshtml           -- logo, Commissions nav, search bar, bell icon
LeadManagementPortal/Views/Customers/Index.cshtml          -- add Edit link (if not already present)

# New static assets (copy from sandbox)
LeadManagementPortal/wwwroot/js/leads-pipeline.js
LeadManagementPortal/wwwroot/js/navbar-search.js
LeadManagementPortal/wwwroot/js/notifications.js
LeadManagementPortal/wwwroot/js/stripe-gradient.js
LeadManagementPortal/wwwroot/css/notifications.css
LeadManagementPortal/wwwroot/css/leads-pipeline.css        -- review: file exists in both, modified

# Modified static assets (selective merge)
LeadManagementPortal/wwwroot/css/site.css                  -- large diff; merge section by section
```

**High-caution files:**
- `_Layout.cshtml` - touches every page; get a second set of eyes
- `site.css` - large; visual regressions are easy to introduce silently

**Validate:**

```powershell
dotnet build
# Manual smoke test: login, dashboard, leads pipeline, notifications bell, global search
```

**Commit:**
```
feat(ui): add pipeline board, login redesign, dashboard overhaul, notification bell, global search
```

---

### Step F - Tests Only (NO CI Workflows)

**Goal:** All new .NET tests land. Tests must be green before PR opens.

> **Why no CI workflows by default?** CI policies for the work repo may be
> cost/compliance-sensitive (private repo minutes, secrets handling, approval
> processes). Do not port `.github/workflows/`, `.github/actions/`, or
> `.github/dependabot.yml` without explicit approval. Do not touch the existing
> `deploy-azure.yml` unless asked.

**Files to add in work repo:**

```
# .NET test classes (copy from sandbox)
LeadManagementPortal.Tests/CommissionsControllerTests.cs
LeadManagementPortal.Tests/CustomerAccessAndUpdateTests.cs
LeadManagementPortal.Tests/CustomerVisibilityHardeningTests.cs
LeadManagementPortal.Tests/FrontendNotificationScriptTests.cs
LeadManagementPortal.Tests/LeadDocumentsControllerTests.cs
LeadManagementPortal.Tests/LeadDocumentServiceDeletionTests.cs
LeadManagementPortal.Tests/LeadExtensionTests.cs
LeadManagementPortal.Tests/LeadsControllerSecurityContractsTests.cs
LeadManagementPortal.Tests/LeadServiceHardeningTests.cs
LeadManagementPortal.Tests/PortabilityMigrationContractsTests.cs
LeadManagementPortal.Tests/SalesOrgAdminVisibilityTests.cs
LeadManagementPortal.Tests/SeedingTool.cs

# Playwright browser tests (copy entire directory - run locally only, NOT in CI)
tests/browser/
```

**Do NOT add:**
```
.github/workflows/*       (approval required)
.github/actions/*         (approval required)
.github/dependabot.yml    (approval required)
```

**Validate locally:**

```powershell
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
# Playwright is run locally by the dev before opening the PR; not required in CI
Set-Location tests/browser
npm ci
npx playwright install

# Option A (default): Playwright starts the app via `webServer` in playwright.config.js
npx playwright test

# Option B: run against an already-running app
# $env:SKIP_WEBSERVER = "1"
# $env:BASE_URL = "https://<staging-host>"
# npx playwright test
```

**Commit:**
```
test: add .NET test suite and Playwright browser tests (local run only)
```

---

## Hard Gates Before Opening the Single PR

All items must be checked off:

- [ ] SQL Server migration validation on a staging clone of the work repo:
  - `dotnet ef database update` applies cleanly (zero errors)
  - `Notifications` table created with correct columns + indexes
  - `LeadFollowUpTasks` table created with correct columns + indexes
  - Rollback (`dotnet ef database update <previous-migration>`) removes tables cleanly
- [ ] `dotnet build` exits 0 on the integration branch
- [ ] `dotnet test` exits 0 (all new test classes + any existing tests pass)
- [ ] No Render-only files present in the PR diff (run `git diff main --name-only` and check)
- [ ] Role/permission regression check completed for:
  - `GrantExtension` (SalesOrgAdmin + OrganizationAdmin only)
  - Lead reassignment (GroupAdmin + OrganizationAdmin only)
  - Lead status movement (`UpdateStatus` - who can move to which statuses)
  - Notification ownership (users can only mark their own notifications read)
- [ ] Manual smoke test completed:
  - [ ] Login -> LoginTransition -> Dashboard renders without error
  - [ ] Leads index loads pipeline board
  - [ ] Follow-up task can be created, completed, deleted
  - [ ] Lead status can be moved via pipeline API
  - [ ] Lead CSV export downloads a valid file
  - [ ] Notification bell shows unread count; mark-read works
  - [ ] Global search returns results
  - [ ] Customer Edit saves and validates correctly
  - [ ] Commissions page loads (mock data acceptable)
- [ ] `MIGRATION_PLAYBOOK.md` and `PORTING_LOG.md` updated in the PR branch

---

## PR Template

Use this body when opening the single PR:

```markdown
## Summary

Port of all product features from `SalesRepPortal-render` sandbox to work repo.
Built through a controlled integration branch with staged commits.

## Port order used

- Step A: Schema (Notifications + LeadFollowUpTasks migrations)
- Step B: Services (notification service, follow-up task methods, customer update/search)
- Step C: Startup + packages (service registration, CsvHelper)
- Step D: Controllers (pipeline API, search, notifications API, customer edit, commissions)
- Step E: Views and assets (pipeline board, login redesign, dashboard, layout overhaul)
- Step F: Tests only (no CI workflows - approval required)

## Schema changes

- Migration `AddNotifications` - creates `Notifications` table (safe, additive)
- Migration `AddLeadFollowUpTasks` - creates `LeadFollowUpTasks` table (safe, additive)
- Both validated on SQL Server staging clone with apply and rollback confirmed.

## Not included

- `render.yaml`
- `Dockerfile`
- **All `.github/workflows/` files** - approval required. Do not change the existing
  Azure deploy workflow unless explicitly directed by the owner.
- `.github/actions/`, `.github/dependabot.yml` - approval required
- SQLite provider branch in `Program.cs`
- `EnsureCreatedAsync()` startup path
- Demo user seeding in `SeedData.cs`
- Per-folder AGENTS.md / CLAUDE.md / GEMINI.md agent scaffolding files
- `appsettings.json` Render/SQLite connection string

## Smoke test checklist

- [ ] Login page renders with animated gradient
- [ ] LoginTransition -> Dashboard redirect works
- [ ] Leads pipeline board loads and moves status
- [ ] Follow-up task CRUD works
- [ ] Lead CSV export downloads
- [ ] Notification bell shows count; mark-read works
- [ ] Global search returns leads + customers
- [ ] Customer edit saves
- [ ] Commissions page loads
- [ ] All .NET tests pass
```

---

## DB-Change Safety Rules (Required for Schema PRs)

1. Treat schema changes as a separate first commit on the integration branch.
2. Regenerate migrations in the work repo - never copy from sandbox.
3. Review generated migration for destructive ops:
   - **Safe by default:** `CreateTable`, `AddColumn`, `CreateIndex`
   - **Block by default:** `DropTable`, `DropColumn`, column type narrowing
4. Validate against a SQL Server staging clone before merge.
5. Verify rollback path (down migration) before production apply.
6. Ship app code and migration together; monitor startup logs for apply errors.

---

## CI Guardrails (Scripts)

### check-portability-guardrails.ps1

Run this in the **sandbox** repo after making changes. It verifies:
- All migration files have proper metadata
- If `LeadFollowUpTask` DbSet exists, a migration creates its table
- If `INotificationService` is used, all notification files + DI registration exist
- PR diffs don't mix Render-only files with product files

```powershell
./scripts/ci/check-portability-guardrails.ps1
```

### portability-audit.ps1

Compares the sandbox against a zip snapshot of the work repo. Reports which files
are new, same, or different. Use this to understand the scope of work before porting.

```powershell
./scripts/ci/portability-audit.ps1 -TargetRepoZipPath .\SalesRepPortal-main.zip -ShowLists
```

### portability-target-dry-run.ps1

**IMPORTANT: This is a VALIDATION tool, not a porting tool.** It copies all
portable files into a temp copy of the work repo and runs `dotnet build` +
`dotnet test` to check whether the sandbox code compiles against the work repo.

It does NOT:
- Create PRs or branches
- Touch your actual work repo
- Apply migrations to any database
- Deploy anything

Use it as a pre-flight check to confirm build compatibility:

```powershell
./scripts/ci/portability-target-dry-run.ps1 -TargetRepoZipPath .\SalesRepPortal-main.zip
```

**Do NOT use this script as a substitute for the feature-by-feature PR approach.**
It copies everything at once (the "bulk" approach rated 9/10 risk). It's only
useful for answering "would all this code compile together?" - not for safely
deploying to production.
