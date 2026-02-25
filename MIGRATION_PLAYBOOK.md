# Migration Playbook (Sandbox -> Work Repo)

This file defines the exact process for moving all feature changes from
`SalesRepPortal-render` into `SalesRepPortal` as a single clean PR.

---

## Latest Compatibility Audit (2026-02-25)

- Sandbox: `D:\GitHub\SalesRepPortal-render`
- Work snapshot: `D:\GitHub\SalesRepPortal-main.zip` (last write: 2026-02-24)
- Files differing (shared path, different content): **119** (107 under `LeadManagementPortal/`)
- Files present only in sandbox: **104+** new source files (plus per-folder agent docs)
- Verdict: sandbox is a full superset; narrow PR slicing or a controlled integration
  branch is required.

---

## Portability Risk Score

| Approach | Risk |
|---|---|
| Bulk "copy everything changed" | **9/10 – Do not do this** |
| Single integration PR using the guided sequence below | **3/10 – Acceptable** |
| Narrow feature-by-feature PRs | **2/10 – Lowest risk** |

---

## What Was Added (Feature Summary)

See `PORTING_LOG.md` for per-feature detail and exact file lists. Summary:

| # | Feature | Schema change? | New package? |
|---|---|---|---|
| 1 | Notification system (bell + API) | YES – `Notifications` table | No |
| 2 | Lead follow-up task system (pipeline board) | YES – `LeadFollowUpTasks` table | No |
| 3 | Lead CSV export | No | YES – CsvHelper 33.0.1 |
| 4 | Global navbar search API + typeahead | No | No |
| 5 | Customer edit (full CRUD) | No | No |
| 6 | Commissions dashboard (mock scaffold) | No | No |
| 7 | Login redesign + transition animation | No | No |
| 8 | Dashboard redesign (stat cards, activity feed) | No | No |
| 9 | Layout/nav overhaul (logo, search, bell) | No | No |
| 10 | Local file storage (dev/Render fallback) | No | No |
| 11 | .NET unit/integration test suite (7 classes) | No | No |
| 12 | Playwright browser test suite | No | No |
| 13 | GitHub Actions CI workflows | No | No | **DO NOT PORT** (private repo = paid minutes) |

---

## Never Port These

- `render.yaml`, `Dockerfile`, `.github/workflows/docker-build.yml`
- `Program.cs` – SQLite provider branch + `EnsureCreatedAsync()` path
- `Data/SeedData.cs` – demo user additions
- `appsettings.json` – SQLite connection string / Render env overrides
- Per-folder `AGENTS.md` / `CLAUDE.md` / `GEMINI.md` docs
- **All GitHub Actions workflows** – the work repo is **private**; GitHub Actions
  minutes are billed on private repos. Do not port any `.github/workflows/` files,
  `.github/actions/`, or `.github/dependabot.yml`. The existing `deploy-azure.yml`
  the boss already has is the only workflow that should exist in the work repo.
  If CI is wanted later, that is a separate conversation with the boss about budget.

---

## Current High-Risk Items

### Risk 1 – EF Schema Mismatch (BLOCKING)

Two new tables exist in sandbox that do not exist in the work repo:

- `Notifications` – required by Features 1, 9 (notification bell in layout)
- `LeadFollowUpTasks` – required by Features 2, 8 (pipeline board, overdue count)

Porting any code that references these tables without the migrations will cause a
runtime crash on first request that touches them.

**Mitigation:** Regenerate both migrations in the work repo and validate against a
SQL Server staging clone before any other code lands.

### Risk 2 – Notification Service Coupling

`LeadsController` injects `INotificationService` and calls `NotifyUserAsync` on
lead status changes. Porting lead pipeline code (Feature 2) without Feature 1 will
cause a DI / compile failure.

**Mitigation:** Always port Feature 1 (notification stack) before Feature 2.

### Risk 3 – CsvHelper Package Missing

The `Export()` action on `LeadsController` uses `CsvHelper`. If the code is ported
but the NuGet package is not added to `.csproj`, the project will not build.

**Mitigation:** Add `CsvHelper 33.0.1` to `.csproj` in the same commit as the
export action.

### Risk 4 – Program.cs Startup Drift

Sandbox `Program.cs` has significant additions:
- `AddHttpContextAccessor()` (needed by `LocalFileStorageService`)
- Conditional `IFileStorageService` registration (Azure vs local)
- `INotificationService` registration
- `LocalStorageOptions` configuration
- `ForwardedHeadersOptions` + `UseForwardedHeaders()` (reverse proxy support)
- `EnsureCreatedAsync()` SQLite branch (Render-only — do not port)
- Fail-fast re-throw on seed error (evaluate before porting)

**Mitigation:** Port `Program.cs` changes surgically, line by line, not as a bulk
replacement.

### Risk 5 – site.css and _Layout.cshtml Surface Area

Both files have extensive changes. Bulk-replacing them risks losing untracked
prod-only customizations and introducing style regressions.

**Mitigation:** Use `git diff` to review each changed section; port selectively.

---

## Integration Approach: Single Clean PR

This is the recommended path when leadership wants one PR in the work repo.

### Step 0 – Set Up Integration Branch in Work Repo

```bash
# In the work repo
git checkout main
git pull
git checkout -b integration/feature-parity-20260225
```

### Step A – Schema Prerequisites

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

```bash
# In work repo
dotnet ef migrations add AddNotifications \
  --project LeadManagementPortal/LeadManagementPortal.csproj

dotnet ef migrations add AddLeadFollowUpTasks \
  --project LeadManagementPortal/LeadManagementPortal.csproj
```

**Validate:**

```bash
dotnet restore
dotnet build
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
```

Run the generated migrations against a SQL Server staging clone and confirm:
- `UPDATE` applies cleanly
- `DOWN` (rollback) removes tables without data loss on other tables

**Commit:**
```
feat(db): add Notifications and LeadFollowUpTasks schema
```

---

### Step B – Service/Model Contracts

**Goal:** All service interfaces and implementations updated; no behavior-breaking
changes to existing methods.

**Files to modify/add in work repo:**

```
# Service interfaces (add new methods only – do not remove existing signatures)
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

```bash
dotnet build
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
```

**Commit:**
```
feat(services): add notification service, follow-up task service methods, customer update/search
```

---

### Step C – Program.cs + Package Updates

**Goal:** New services are registered; CsvHelper package added; local storage
fallback wired. SQLite-only code stays out.

**Changes to make in work repo:**

```csharp
// Program.cs – add these registrations (do NOT add SQLite/EnsureCreated logic)

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

```bash
dotnet restore
dotnet build
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
```

**Commit:**
```
feat(startup): register notification service, conditional file storage, CsvHelper package
```

---

### Step D – Controllers

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

```bash
dotnet build
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
```

**Commit:**
```
feat(controllers): add pipeline status/follow-up endpoints, notification API, search API, customer edit, commissions scaffold
```

---

### Step E – Views and Frontend Assets

**Goal:** All UI changes land in one commit group; reviewers can see the full
visual scope.

**Files to add/modify in work repo:**

```
# New views (copy from sandbox)
LeadManagementPortal/Views/Account/LoginTransition.cshtml
LeadManagementPortal/Views/Commissions/Index.cshtml
LeadManagementPortal/Views/Customers/Edit.cshtml

# Modified views (careful diff/merge – do not bulk replace)
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
- `_Layout.cshtml` – touches every page; get a second set of eyes
- `site.css` – large; visual regressions are easy to introduce silently

**Validate:**

```bash
dotnet build
# Manual smoke test: login, dashboard, leads pipeline, notifications bell, global search
```

**Commit:**
```
feat(ui): add pipeline board, login redesign, dashboard overhaul, notification bell, global search
```

---

### Step F – Tests Only (NO CI Workflows)

**Goal:** All new .NET tests land. Tests must be green before PR opens.

> **Why no CI workflows?** The work repo is **private**. GitHub Actions minutes are
> billed on private repos. Porting the CI suite would start charging the boss's
> account without his explicit approval. Leave all `.github/workflows/`,
> `.github/actions/`, and `.github/dependabot.yml` out of this PR entirely.
> The existing `deploy-azure.yml` must not be touched. If automated CI is wanted,
> raise that as a separate cost discussion before doing anything.

**Files to add in work repo:**

```
# .NET test classes (copy from sandbox)
LeadManagementPortal.Tests/CustomerAccessAndUpdateTests.cs
LeadManagementPortal.Tests/CustomerVisibilityHardeningTests.cs
LeadManagementPortal.Tests/FrontendNotificationScriptTests.cs
LeadManagementPortal.Tests/LeadExtensionTests.cs
LeadManagementPortal.Tests/LeadsControllerSecurityContractsTests.cs
LeadManagementPortal.Tests/PortabilityMigrationContractsTests.cs
LeadManagementPortal.Tests/SalesOrgAdminVisibilityTests.cs
LeadManagementPortal.Tests/SeedingTool.cs

# Playwright browser tests (copy entire directory – run locally only, NOT in CI)
tests/browser/
```

**Do NOT add:**
```
.github/workflows/*       ← private repo = paid minutes, boss has not approved
.github/actions/*         ← same
.github/dependabot.yml    ← same
```

**Validate locally:**

```bash
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
# Playwright is run locally by the dev before opening the PR; not required in CI
cd tests/browser && npx playwright test  # point PLAYWRIGHT_BASE_URL at local instance
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
- [ ] `dotnet test` exits 0 (all 7 new test classes + any existing tests pass)
- [ ] No Render-only files present in the PR diff (run `git diff main --name-only` and check)
- [ ] Role/permission regression check completed for:
  - `GrantExtension` (SalesOrgAdmin + OrganizationAdmin only)
  - Lead reassignment (GroupAdmin + OrganizationAdmin only)
  - Lead status movement (`UpdateStatus` — who can move to which statuses)
  - Notification ownership (users can only mark their own notifications read)
- [ ] Manual smoke test completed:
  - [ ] Login → LoginTransition → Dashboard renders without error
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
- Step F: Tests only (no CI workflows — private repo, paid minutes)

## Schema changes

- Migration `AddNotifications` – creates `Notifications` table (safe, additive)
- Migration `AddLeadFollowUpTasks` – creates `LeadFollowUpTasks` table (safe, additive)
- Both validated on SQL Server staging clone with apply and rollback confirmed.

## Not included

- `render.yaml`
- `Dockerfile`
- **All `.github/workflows/` files** — work repo is private; Actions minutes are
  billed. Only the existing `deploy-azure.yml` remains. If CI is ever wanted,
  that requires a separate budget conversation with the owner.
- `.github/actions/`, `.github/dependabot.yml` — same reason
- SQLite provider branch in `Program.cs`
- `EnsureCreatedAsync()` startup path
- Demo user seeding in `SeedData.cs`
- Per-folder AGENTS.md / CLAUDE.md / GEMINI.md agent scaffolding files
- `appsettings.json` Render/SQLite connection string

## Smoke test checklist

- [ ] Login page renders with animated gradient
- [ ] LoginTransition → Dashboard redirect works
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
2. Regenerate migrations in the work repo — never copy from sandbox.
3. Review generated migration for destructive ops:
   - **Safe by default:** `CreateTable`, `AddColumn`, `CreateIndex`
   - **Block by default:** `DropTable`, `DropColumn`, column type narrowing
4. Validate against a SQL Server staging clone before merge.
5. Verify rollback path (down migration) before production apply.
6. Ship app code and migration together; monitor startup logs for apply errors.

---

## CI Guardrails (Scripts)

```powershell
# Always-on portability check
./scripts/ci/check-portability-guardrails.ps1

# Optional dry run against the work repo zip
./scripts/ci/portability-target-dry-run.ps1 -TargetRepoZipPath "D:\GitHub\SalesRepPortal-main.zip"
```
