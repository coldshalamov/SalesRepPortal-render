# Porting Log (Render Sandbox -> Work Repo)

Purpose: track every change made in `SalesRepPortal-render`, classify whether it
should be ported to the work repo, and define a safe PR workflow. Entries are
grouped by feature (not by commit) because several features span many commits.

## Rules

1. Never port Render deploy artifacts unless explicitly approved.
2. Migrations: never copy migration files from sandbox; regenerate in the work repo.
3. Avoid "copy everything changed" PRs. Slice by feature.
4. Treat SQL Server vs SQLite behavior as provider-sensitive; verify on SQL Server.

## Classification Legend

| Label | Meaning |
|---|---|
| **Portable** | Port as-is with minimal review |
| **Portable with edits** | Port after stripping sandbox-only behavior/config |
| **Render-only** | Never port (Render sandbox infrastructure/runtime) |
| **Approval required** | Technically portable, but requires explicit approval (policy/cost/secrets) |

---

## AUDIT-2026-02-25 - Compatibility scan vs `D:\GitHub\SalesRepPortal-render\SalesRepPortal-main.zip`

Work snapshot zip details:
- Path: `D:\GitHub\SalesRepPortal-render\SalesRepPortal-main.zip`
- Last write: 2026-02-24 22:41:36
- SHA256: `32DFBB55EA654DE999E4B2CC1A0E0B921291D778559377C0B60169F7375E9DFC`

Sandbox:
- Repo root: `D:\GitHub\SalesRepPortal-render`
- HEAD at time of audit: `634473171a1eb1662dc778aa179f54d8dbbb72a8`

Portable-file diff (normalized line endings for text files):
- Total portable files considered: **150**
- Present only in sandbox: **39**
- Same content: **62**
- Different content: **49**

Reproduce:
```powershell
./scripts/ci/portability-audit.ps1 -TargetRepoZipPath .\SalesRepPortal-main.zip
```

Notes:
- This "portable-file diff" intentionally focuses on code and tooling that is normally portable:
  `LeadManagementPortal/`, `LeadManagementPortal.Tests/`, `LeadManagementPortal.sln`, `scripts/ci/`.
- It intentionally excludes: `.github/`, `tests/browser/`, `render.yaml`, `Dockerfile`, `.render/`, runtime uploads,
  and app build outputs.

Cross-cutting "different content" files (sandbox vs zip) that must be reviewed carefully and not accidentally swept
into unrelated feature PRs:
- Startup and config: `LeadManagementPortal/Program.cs`, `LeadManagementPortal/appsettings.json`
- Data/EF: `LeadManagementPortal/Data/ApplicationDbContext.cs`, `LeadManagementPortal/Data/SeedData.cs`,
  `LeadManagementPortal/Migrations/ApplicationDbContextModelSnapshot.cs`
- Packages: `LeadManagementPortal/LeadManagementPortal.csproj`
- UI surface area: `LeadManagementPortal/Views/Shared/_Layout.cshtml`, `LeadManagementPortal/wwwroot/css/site.css`,
  many views under `LeadManagementPortal/Views/`
- Misc frontend JS/assets: `LeadManagementPortal/wwwroot/js/site.js`, `LeadManagementPortal/wwwroot/js/multiselect.js`,
  `LeadManagementPortal/wwwroot/img/*`

---

## Feature Inventory

### Feature 1 - Notification System (full stack)

**What it does:** In-app bell-icon notifications. Supports user-targeted and
role-broadcast delivery (e.g., "lead assigned to you" vs "all admins").

**API:** `GET /api/notifications/get_notifications?limit=50&include_unread_count=true` (polling).
`GET /api/notifications/get_unread_count` exists but the JS poller does not call it.

**New files (sandbox-only):**

| File | Note |
|---|---|
| `LeadManagementPortal/Models/Notification.cs` | DB entity; supports `UserId` or `Role` targeting |
| `LeadManagementPortal/Services/INotificationService.cs` | Service contract |
| `LeadManagementPortal/Services/NotificationService.cs` | EF implementation |
| `LeadManagementPortal/Controllers/NotificationsApiController.cs` | REST API under `/api/notifications` |
| `LeadManagementPortal/Migrations/20260224_AddNotifications.cs` | Sandbox migration (do not port as-is) |
| `LeadManagementPortal/wwwroot/css/notifications.css` | Bell dropdown styles |
| `LeadManagementPortal/wwwroot/js/notifications.js` | Polling + dropdown JS (30s interval) |

**Modified files:**

| File | Change |
|---|---|
| `LeadManagementPortal/Data/ApplicationDbContext.cs` | `DbSet<Notification>` + `OnModelCreating` config |
| `LeadManagementPortal/Views/Shared/_Layout.cshtml` | Bell icon + badge + script/style includes |
| `LeadManagementPortal/Program.cs` | DI registration for `INotificationService` |

**Schema:** `Notifications` table (required before any UI that calls the API).

**Classification:** Portable with edits

**Port action (work repo):**
1. Port model/service/controller/CSS/JS/layout changes.
2. Port DbContext additions.
3. Regenerate migration in the work repo (SQL Server): `dotnet ef migrations add AddNotifications ...`.

---

### Feature 2 - Lead Follow-Up Task System (pipeline board)

**What it does:** Kanban-style pipeline board on Leads index. Leads can have
typed follow-up tasks with optional due dates. Includes API endpoints used by
`leads-pipeline.js`.

**New files (sandbox-only):**

| File | Note |
|---|---|
| `LeadManagementPortal/Models/LeadFollowUpTask.cs` | EF entity |
| `LeadManagementPortal/Migrations/20260225_AddLeadFollowUpTasks.cs` | Sandbox migration (do not port as-is) |
| `LeadManagementPortal/wwwroot/js/leads-pipeline.js` | Board + follow-up sidebar JS |
| `LeadManagementPortal/wwwroot/css/leads-pipeline.css` | Board + pipeline styles |

**Modified files:**

| File | Change |
|---|---|
| `LeadManagementPortal/Models/Lead.cs` | Added `ICollection<LeadFollowUpTask> FollowUpTasks` navigation property |
| `LeadManagementPortal/Data/ApplicationDbContext.cs` | `DbSet<LeadFollowUpTask>` + config |
| `LeadManagementPortal/Services/ILeadService.cs` | Follow-up and search contract additions |
| `LeadManagementPortal/Services/LeadService.cs` | Implements new follow-up/search methods |
| `LeadManagementPortal/Controllers/LeadsController.cs` | Adds follow-up/task endpoints and pipeline status endpoints |
| `LeadManagementPortal/Views/Leads/Index.cshtml` | Pipeline board UI (large change) |

**Schema:** `LeadFollowUpTasks` table (required before any follow-up calls).

**Classification:** Portable with edits (schema required first)

**Dependency:** If `LeadsController` calls `INotificationService` on status changes,
port Feature 1 first to avoid DI/build failures.

---

### Feature 3 - Lead CSV Export

**What it does:** `GET /Leads/Export` downloads a CSV of visible leads. Uses CsvHelper.

**Columns (current sandbox):**
`FirstName`, `LastName`, `Email`, `Phone`, `Company`, `Address`, `City`, `State`,
`ZipCode`, `Status`, `UrgencyLevel`, `DaysRemaining`, `CreatedDate`, `ExpiryDate`,
`AssignedTo`, `Notes`.

**Modified files:**

| File | Change |
|---|---|
| `LeadManagementPortal/Controllers/LeadsController.cs` | Adds `Export()` + `LeadExportRow` |
| `LeadManagementPortal/LeadManagementPortal.csproj` | Adds `CsvHelper` 33.0.1 |
| `LeadManagementPortal/Views/Leads/Index.cshtml` | (If ported) includes export button |

**Classification:** Portable

---

### Feature 4 - Global Navbar Search (typeahead)

**What it does:** Typeahead search bar in the top nav. Queries `GET /api/search?q=...`
and returns up to 5 leads + 5 customers (role-scoped).

**New files (sandbox-only):**

| File | Note |
|---|---|
| `LeadManagementPortal/Controllers/SearchController.cs` | `GET /api/search?q=` |
| `LeadManagementPortal/wwwroot/js/navbar-search.js` | Typeahead + dropdown |

**Modified files:**

| File | Change |
|---|---|
| `LeadManagementPortal/Services/ILeadService.cs` | `SearchTopAsync` |
| `LeadManagementPortal/Services/LeadService.cs` | `SearchTopAsync` implementation |
| `LeadManagementPortal/Services/ICustomerService.cs` | `SearchTopAsync` |
| `LeadManagementPortal/Services/CustomerService.cs` | `SearchTopAsync` implementation |
| `LeadManagementPortal/Views/Shared/_Layout.cshtml` | Search input + JS include |

**Classification:** Portable

---

### Feature 5 - Customer Edit

**What it does:** Adds an Edit flow for existing customers (previously create-only).
Includes org-boundary validation when reassigning reps.

**New files (sandbox-only):**

| File | Note |
|---|---|
| `LeadManagementPortal/Models/ViewModels/CustomerEditViewModel.cs` | Edit form model |
| `LeadManagementPortal/Views/Customers/Edit.cshtml` | Edit form view |

**Modified files:**

| File | Change |
|---|---|
| `LeadManagementPortal/Services/ICustomerService.cs` | Adds `GetAccessibleByIdAsync`, `UpdateAsync` |
| `LeadManagementPortal/Services/CustomerService.cs` | Implements above |
| `LeadManagementPortal/Controllers/CustomersController.cs` | Adds `Edit(GET)` + `Edit(POST)` |
| `LeadManagementPortal/Views/Customers/Index.cshtml` | (If ported) edit links/buttons |
| `LeadManagementPortal/Views/Customers/Details.cshtml` | (If ported) edit links/buttons |

**Classification:** Portable

---

### Feature 6 - Commissions Dashboard (scaffold)

**What it does:** Adds a "Commissions" page (currently mock/scaffold UI).

**New files (sandbox-only):**

| File | Note |
|---|---|
| `LeadManagementPortal/Controllers/CommissionsController.cs` | Index returns mock data |
| `LeadManagementPortal/Models/ViewModels/CommissionDashboardViewModel.cs` | ViewModel |
| `LeadManagementPortal/Views/Commissions/Index.cshtml` | UI |

**Modified files:**

| File | Change |
|---|---|
| `LeadManagementPortal/Views/Shared/_Layout.cshtml` | Adds nav item |

**Classification:** Portable with edits

**Port caveat:** Decide whether to ship mock data. Consider a banner/feature flag.

---

### Feature 7 - Login Redesign + Transition Animation

**What it does:** Login UI overhaul and a post-login transition page.

**New files (sandbox-only):**

| File | Note |
|---|---|
| `LeadManagementPortal/Views/Account/LoginTransition.cshtml` | Transition page |
| `LeadManagementPortal/wwwroot/js/stripe-gradient.js` | Animated gradient |

**Modified files:**

| File | Change |
|---|---|
| `LeadManagementPortal/Views/Account/Login.cshtml` | Redesigned login UI |
| `LeadManagementPortal/Controllers/AccountController.cs` | Redirects via LoginTransition |
| `LeadManagementPortal/wwwroot/css/site.css` | Login/animation styles |

**Classification:** Portable with edits

---

### Feature 8 - Dashboard Redesign

**What it does:** Updates dashboard UI (stat cards, activity feed, pipeline summary).

**Modified files:**

| File | Change |
|---|---|
| `LeadManagementPortal/Views/Dashboard/Index.cshtml` | Redesigned |
| `LeadManagementPortal/wwwroot/css/site.css` | Dashboard styles |

**Dependency:** If you surface overdue follow-up counts, Feature 2 must land first.

**Classification:** Portable with edits

---

### Feature 9 - Layout / Navigation Overhaul

**What it does:** `_Layout.cshtml` overhaul with dynamic org branding (logos),
responsive nav, and wiring for notification bell + global search.

**Modified files:**

| File | Change |
|---|---|
| `LeadManagementPortal/Views/Shared/_Layout.cshtml` | Large change |
| `LeadManagementPortal/wwwroot/css/site.css` | Navbar styles |
| `LeadManagementPortal/wwwroot/img/DiRxLogo.svg` | Updated asset |
| `LeadManagementPortal/wwwroot/img/DiRxLogoWhite.svg` | Updated asset |

**Classification:** Portable with edits

**Port caveat:** The dynamic logo query runs inline in the view; consider a ViewComponent later.

---

### Feature 10 - Local File Storage Fallback + Secure Downloads

**What it does:** Adds a local-disk `IFileStorageService` fallback when Azure
storage is not configured, and serves files via time-limited signed tokens.

**New files (sandbox-only):**

| File | Note |
|---|---|
| `LeadManagementPortal/Models/LocalStorageOptions.cs` | Local storage options |
| `LeadManagementPortal/Services/LocalFileStorageService.cs` | Local disk storage implementation |
| `LeadManagementPortal/Controllers/FilesController.cs` | `/files/{token}` endpoint |

**Modified files:**

| File | Change |
|---|---|
| `LeadManagementPortal/Services/IFileStorageService.cs` | Storage contract changes |
| `LeadManagementPortal/Services/AzureBlobStorageService.cs` | Aligns with contract + token URL behavior |
| `LeadManagementPortal/Services/ILeadDocumentService.cs` | Uses storage abstraction |
| `LeadManagementPortal/Services/LeadDocumentService.cs` | Uses storage abstraction |
| `LeadManagementPortal/Controllers/LeadDocumentsController.cs` | Uses storage abstraction + secure download URLs |
| `LeadManagementPortal/Program.cs` | Conditional registration (Azure vs local) |

**Classification:** Portable with edits

**Port caveats:**
- DataProtection keys must be persisted if you run multiple instances or need tokens to survive restarts.
- Do not port sandbox `appsettings.json` into the work repo; add local storage config via env vars or dev-only config.

---

### Feature 11 - Expanded .NET Test Suite

**What it does:** Adds unit-ish tests to harden access control and portability contracts.

**New files (sandbox-only, `LeadManagementPortal.Tests/`):**
- `LeadManagementPortal.Tests/CommissionsControllerTests.cs`
- `LeadManagementPortal.Tests/CustomerAccessAndUpdateTests.cs`
- `LeadManagementPortal.Tests/CustomerVisibilityHardeningTests.cs`
- `LeadManagementPortal.Tests/FrontendNotificationScriptTests.cs`
- `LeadManagementPortal.Tests/LeadDocumentsControllerTests.cs`
- `LeadManagementPortal.Tests/LeadDocumentServiceDeletionTests.cs`
- `LeadManagementPortal.Tests/LeadExtensionTests.cs`
- `LeadManagementPortal.Tests/LeadsControllerSecurityContractsTests.cs`
- `LeadManagementPortal.Tests/LeadServiceHardeningTests.cs`
- `LeadManagementPortal.Tests/PortabilityMigrationContractsTests.cs`
- `LeadManagementPortal.Tests/SeedingTool.cs`

**Modified files:**
- `LeadManagementPortal.Tests/SalesOrgAdminVisibilityTests.cs` (tightened visibility contracts)

**Classification:** Portable (strongly recommended)

---

### Feature 12 - Playwright Browser Test Suite

**What it does:** End-to-end browser tests using Playwright. Covers public pages,
authenticated flows, API surface parity, mobile layout, and basic advisory checks.

**Files (sandbox-only):**
- `tests/browser/*`

**How it runs (current sandbox):**
- By default, `tests/browser/playwright.config.js` starts the app via Playwright `webServer`
  (`dotnet run`) and uses SQLite under `tests/browser/.tmp/`.
- To run against an external deployed URL, set `BASE_URL` and `SKIP_WEBSERVER=1`.

**Classification:** Portable (often best as a separate PR)

---

### Feature 13 - GitHub Actions CI Workflows

**What it does:** Adds CI workflows for build/test and optional browser parity checks.

**Files (sandbox-only):**
- `.github/*`

**Classification:** Approval required

**Port notes:**
- Do not change existing Azure deploy workflows in the work repo without explicit direction.
- If approved, port the minimal workflows first (build + test) and add browser/CodeQL later.

---

## Render-Only / Not-To-Port Items

Never port these without explicit approval:
- `render.yaml`, `Dockerfile`, `.render/`, `RENDER.md`
- `LeadManagementPortal/appsettings.json` (contains environment-specific config and may contain secrets)
- SQLite-only startup behavior in `LeadManagementPortal/Program.cs` (`DatabaseProvider=Sqlite`, `EnsureCreatedAsync()`)
- Any `/tmp/...` paths or Render-specific environment variables

Evaluate before porting (depends on work repo infrastructure):
- `UseForwardedHeaders()` / `ForwardedHeadersOptions` (reverse proxy correctness)
- "Fail-fast" behavior on seed failure (startup behavior change)

---

## Safe Port Workflow (Per Feature)

1. Pick a single feature from this log.
2. In the work repo, create a feature branch.
3. Port only the files for that feature (avoid bulk directory copies).
4. If the feature adds EF entities, regenerate migrations in the work repo.
5. Verify in work repo (PowerShell):
```powershell
dotnet tool restore
dotnet restore
dotnet build
dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj
```
6. Open PR and include a "Not included" section (explicitly list Render-only items excluded).

---

## Suggested Feature PR Stack (Leadership-Friendly)

1. PR: DB prerequisites (Notifications + LeadFollowUpTasks migrations, SQL review)
2. PR: Notifications (API/service/layout/JS/CSS + minimal tests)
3. PR: Follow-up tasks / pipeline board
4. PR: Storage fallback + secure downloads
5. PR: Search + customer edit
6. PR: Commissions scaffold
7. PR: UI polish (login/dashboard/layout/site.css) - optional, large diff
8. PR: Quality gates (LeadManagementPortal.Tests + scripts/ci)
9. Optional PRs (approval required): Playwright `tests/browser/*`, GitHub Actions `.github/*`, Render deploy artifacts
