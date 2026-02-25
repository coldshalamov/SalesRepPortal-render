# Porting Log (Render Sandbox -> Work Repo)

Purpose: track every change made in `SalesRepPortal-render`, classify whether it
should be ported to the work repo, and define a safe PR workflow. Entries are
grouped by feature, not by commit, because several features span many commits.

## Rules

1. Never port `Render-only` commits to the work repo.
2. Port only selected files/commits to a dedicated feature branch in the work repo.
3. Keep infrastructure changes (Render, SQLite, demo seeding) out of work PRs unless explicitly approved.
4. Migrations: **never copy migration files from sandbox; regenerate in the work repo.**

## Classification Legend

| Label | Meaning |
|---|---|
| **Portable** | Port as-is with minimal review |
| **Portable with edits** | Port after stripping sandbox-only code |
| **Render-only** | Never port |

---

## AUDIT-2026-02-25 – Compatibility scan vs `D:\GitHub\SalesRepPortal-main.zip`

- Snapshot date: 2026-02-24
- Files differing (same path, different content): **119** (107 under `LeadManagementPortal/`)
- Files present only in sandbox: **104+** (new source files + AGENTS/CLAUDE/GEMINI docs per folder)
- Conclusion: sandbox is a full superset; heavy drift exists; narrow PR slicing is required.

---

## Feature Inventory

### Feature 1 – Notification System (full stack)

**What it does:** In-app bell-icon notifications. Supports user-specific and
role-broadcast delivery (e.g. "lead assigned to you" vs "all admins"). Frontend
polls `GET /api/notifications/get_unread_count` on page load; dropdown fetches and
renders items; mark-read/unread/all-read supported.

**New files (sandbox-only):**

| File | Note |
|---|---|
| `LeadManagementPortal/Models/Notification.cs` | DB entity; supports UserId or Role targeting |
| `LeadManagementPortal/Services/INotificationService.cs` | Full service contract |
| `LeadManagementPortal/Services/NotificationService.cs` | EF implementation |
| `LeadManagementPortal/Controllers/NotificationsApiController.cs` | REST API at `/api/notifications` |
| `LeadManagementPortal/Migrations/20260224_AddNotifications.cs` | Creates `Notifications` table |
| `LeadManagementPortal/wwwroot/css/notifications.css` | Bell dropdown styles |
| `LeadManagementPortal/wwwroot/js/notifications.js` | Polling + dropdown JS |

**Modified files:**

| File | Change |
|---|---|
| `Data/ApplicationDbContext.cs` | `DbSet<Notification> Notifications` + `OnModelCreating` config |
| `Views/Shared/_Layout.cshtml` | Bell icon, unread badge, `notifications.css`/`notifications.js` injected |
| `Program.cs` | `AddScoped<INotificationService, NotificationService>()` |

**Schema:** `Notifications` table; FK to `AspNetUsers.Id` (CASCADE DELETE); indexes
on `UserId`, `Role`, `IsRead`, `CreatedAt`.

**Classification:** Portable with edits
**Port action:**
1. Port model + service + controller files.
2. Port `ApplicationDbContext` additions.
3. **Regenerate migration in work repo**: `dotnet ef migrations add AddNotifications`.
4. Port CSS/JS assets and `_Layout.cshtml` bell-icon additions.
5. Register service in `Program.cs`.
6. Do NOT copy the migration `.cs` file from sandbox.

---

### Feature 2 – Lead Follow-Up Task System (full stack)

**What it does:** Kanban-style pipeline board on the Leads index. Each lead can
have typed follow-up tasks (call, email, demo, etc.) with due dates. API endpoints
let the frontend create/complete/delete tasks without full-page reloads. Overdue
count shown as a pipeline stat.

**New files (sandbox-only):**

| File | Note |
|---|---|
| `LeadManagementPortal/Models/LeadFollowUpTask.cs` | Entity: Type, Description, DueDate, IsCompleted, CreatedById, CompletedById |
| `LeadManagementPortal/Migrations/20260225_AddLeadFollowUpTasks.cs` | Creates `LeadFollowUpTasks` table |
| `LeadManagementPortal/wwwroot/js/leads-pipeline.js` | Board JS (drag-and-drop status, follow-up task sidebar) |
| `LeadManagementPortal/wwwroot/css/leads-pipeline.css` | Board and pipeline styles |

**Modified files:**

| File | Change |
|---|---|
| `Models/Lead.cs` | Added `ICollection<LeadFollowUpTask> FollowUpTasks` navigation property |
| `Services/ILeadService.cs` | New methods: `SearchTopAsync`, `GetFollowUpsForLeadsAsync`, `GetFollowUpsForLeadAsync`, `AddFollowUpAsync`, `CompleteFollowUpAsync`, `DeleteFollowUpsAsync`, `GetOverdueFollowUpCountAsync` |
| `Services/LeadService.cs` | Implementations of all above |
| `Controllers/LeadsController.cs` | New API actions: `UpdateStatus`, `AddFollowUp`, `CompleteFollowUp`, `DeleteFollowUps`; also `Export` (CSV) |
| `Data/ApplicationDbContext.cs` | `DbSet<LeadFollowUpTask> LeadFollowUpTasks` + `OnModelCreating` config |
| `Views/Leads/Index.cshtml` | Kanban board view, pipeline stat panel, follow-up task sidebar |

**Schema:** `LeadFollowUpTasks` table; FK to `Leads.Id` (CASCADE DELETE); indexes on
`LeadId`, `DueDate`, `IsCompleted`.

**Classification:** Portable with edits (high attention — schema required first)
**Port action:**
1. Port `LeadFollowUpTask.cs` model.
2. Port `Lead.cs` navigation property addition.
3. Port `ApplicationDbContext` additions.
4. **Regenerate migration in work repo**: `dotnet ef migrations add AddLeadFollowUpTasks`.
5. Port `ILeadService` + `LeadService` method additions.
6. Port `LeadsController` new API actions.
7. Port JS/CSS assets.
8. Port `Views/Leads/Index.cshtml` (large change – review carefully for unrelated diffs).
9. Do NOT copy the migration `.cs` file from sandbox.

**Dependency:** Feature 1 (Notifications) must be in place first because
`LeadsController` now injects `INotificationService` and calls `NotifyUserAsync`
on status changes.

---

### Feature 3 – Lead CSV Export

**What it does:** `GET /Leads/Export` downloads a CSV of the current user's visible
leads. Uses CsvHelper. Output columns: Name, Email, Phone, Company, Status,
Assigned Rep, Sales Group, Created Date, Expiry Date, Days Remaining.

**New/modified files:**

| File | Change |
|---|---|
| `Controllers/LeadsController.cs` | New `Export()` action + `LeadExportRow` inner class |
| `LeadManagementPortal.csproj` | `CsvHelper` 33.0.1 package added |

**Classification:** Portable
**Port action:**
1. Add `<PackageReference Include="CsvHelper" Version="33.0.1" />` to work repo `.csproj`.
2. Port the `Export()` method and `LeadExportRow` class from `LeadsController.cs`.
3. Add the export button to `Views/Leads/Index.cshtml` if porting the full view.

---

### Feature 4 – Global Navbar Search

**What it does:** Typeahead search bar in the top nav. Queries
`GET /api/search?q=...` which returns up to 5 leads + 5 customers matching the
term, respecting role-based visibility. Results are shown in a dropdown; clicking
navigates to the detail page.

**New files (sandbox-only):**

| File | Note |
|---|---|
| `Controllers/SearchController.cs` | `GET /api/search?q=` – returns `{ leads:[...], customers:[...] }` |
| `wwwroot/js/navbar-search.js` | Typeahead input, debounce, dropdown rendering |

**Modified files:**

| File | Change |
|---|---|
| `Services/ILeadService.cs` | `SearchTopAsync` (top-N fast search for typeahead, no audit log spam) |
| `Services/ICustomerService.cs` | `SearchTopAsync` (same) |
| `Views/Shared/_Layout.cshtml` | Search input in navbar + `navbar-search.js` injected |

**Classification:** Portable
**Port action:**
1. Port `SearchController.cs`.
2. Port `SearchTopAsync` additions to both service interfaces + implementations.
3. Port navbar search markup + script include from `_Layout.cshtml`.
4. Port `navbar-search.js`.

---

### Feature 5 – Customer Edit

**What it does:** Allows editing an existing customer's contact info and reassigning
the sales rep. Guards ensure the new rep is in the same sales org. Previously,
customers could not be edited after creation.

**New files (sandbox-only):**

| File | Note |
|---|---|
| `Models/ViewModels/CustomerEditViewModel.cs` | Strongly-typed edit form model |
| `Views/Customers/Edit.cshtml` | Edit form view |

**Modified files:**

| File | Change |
|---|---|
| `Services/ICustomerService.cs` | Added `GetAccessibleByIdAsync`, `UpdateAsync` |
| `Services/CustomerService.cs` | Implementations |
| `Controllers/CustomersController.cs` | New `Edit(GET)` + `Edit(POST)` actions with org-boundary validation |

**Classification:** Portable
**Port action:**
1. Port `CustomerEditViewModel.cs`.
2. Port `ICustomerService` + `CustomerService` additions.
3. Port `CustomersController` Edit actions.
4. Port `Views/Customers/Edit.cshtml`.
5. Add "Edit" link to `Views/Customers/Index.cshtml` / `Details.cshtml` if desired.

---

### Feature 6 – Commissions Dashboard (scaffold / mock data)

**What it does:** Adds a "Commissions" page in the nav accessible to all
authenticated users. Currently displays hardcoded mock data (no DB). Intended as
a scaffold for a real commissions-from-orders integration.

**New files (sandbox-only):**

| File | Note |
|---|---|
| `Controllers/CommissionsController.cs` | Index() returns mock `CommissionDashboardViewModel` |
| `Models/ViewModels/CommissionDashboardViewModel.cs` | TotalEarned, CurrentMonth, Pending, RecentDeals list |
| `Views/Commissions/Index.cshtml` | Commission dashboard UI |

**Modified files:**

| File | Change |
|---|---|
| `Views/Shared/_Layout.cshtml` | "Commissions" nav item added |

**Classification:** Portable with edits
**Port caveat:** Currently uses mock data. Port as a scaffold only; do not imply to
users or QA that this is live data. Add a "mock data" banner or disable the nav
item until real data is wired.
**Port action:**
1. Port all three files.
2. Add nav item to `_Layout.cshtml`.
3. Decide before PR whether to show mock data or hide behind a feature flag.

---

### Feature 7 – Login Page Redesign + Transition Animation

**What it does:** Complete visual overhaul of the login screen. Adds an animated
stripe-gradient background, a branded card layout, and an intermediate
`LoginTransition.cshtml` page that plays a loading animation before redirecting
to the dashboard. Improves first-impression UX significantly.

**New files (sandbox-only):**

| File | Note |
|---|---|
| `Views/Account/LoginTransition.cshtml` | Intermediate animated transition page |
| `wwwroot/js/stripe-gradient.js` | Animated mesh-gradient background JS |

**Modified files:**

| File | Change |
|---|---|
| `Views/Account/Login.cshtml` | Fully redesigned (gradient bg, card, branded layout) |
| `Controllers/AccountController.cs` | POST login now redirects to `LoginTransition` instead of `Dashboard/Index` directly |
| `wwwroot/css/site.css` | Major additions for login card, gradient, animations |

**Classification:** Portable with edits
**Port action:**
1. Port `LoginTransition.cshtml` and `stripe-gradient.js`.
2. Port redesigned `Login.cshtml`.
3. Port `AccountController.cs` redirect change (only that block — review for any
   other unrelated changes in the controller).
4. Merge `site.css` additions carefully (high risk of style regression if blindly
   replaced; do a selective merge of the new login/animation sections).

---

### Feature 8 – Dashboard Redesign

**What it does:** Replaces the plain dashboard with stat cards (total leads, new
this week, converted, expiring soon), a recent activity feed, and a pipeline
summary panel. Layout uses CSS Grid. Also surfaces overdue follow-up count from
`ILeadService.GetOverdueFollowUpCountAsync`.

**Modified files:**

| File | Change |
|---|---|
| `Views/Dashboard/Index.cshtml` | Fully redesigned (stat cards, activity feed, grid layout) |
| `Controllers/LeadsController.cs` | Sets `ViewBag.OverdueFollowUpCount` in `Index()` (consumed by Leads view, not Dashboard directly) |
| `wwwroot/css/site.css` | Dashboard card/grid CSS additions |

**Classification:** Portable with edits
**Dependency:** Overdue follow-up count requires Feature 2 (follow-up tasks) to be
in place. If porting dashboard without Feature 2, guard the `ViewBag.OverdueFollowUpCount`
call with a null check.

---

### Feature 9 – Layout / Navigation Overhaul

**What it does:** `_Layout.cshtml` redesigned with dynamic org branding (pulls logo
from the user's `SalesOrg` or `SalesGroup`), updated nav structure, responsive
improvements, and wiring for notification bell + global search.

**Modified files:**

| File | Change |
|---|---|
| `Views/Shared/_Layout.cshtml` | Dynamic logo, Commissions item, search bar, notification bell, responsive nav |
| `wwwroot/css/site.css` | Navbar custom styles, color palette, font imports |

**Classification:** Portable with edits
**Port action:** This is the highest surface-area view change in the PR. Review
diff section-by-section. The dynamic logo query (`AppDbContext.Users.Include(...)`)
runs inline in the view — this is acceptable but note it fires a synchronous DB
query per page load; consider converting to a ViewComponent in a follow-up.

---

### Feature 10 – Local File Storage + Secure File Download (dev/Render fallback)

**What it does:** When Azure Blob Storage is not configured, `Program.cs` registers
`LocalFileStorageService` instead of `AzureBlobStorageService`. Files are stored
on disk. `FilesController` serves them via cryptographically signed, time-limited
tokens (ASP.NET Data Protection API).

**New files (sandbox-only):**

| File | Note |
|---|---|
| `Models/LocalStorageOptions.cs` | Config POCO: `RootPath`, `BaseUrlPath` |
| `Services/LocalFileStorageService.cs` | `IFileStorageService` impl for local disk |
| `Controllers/FilesController.cs` | `/files/{token}` secure download endpoint |

**Modified files:**

| File | Change |
|---|---|
| `Program.cs` | Conditional `IFileStorageService` registration (Azure if configured, local otherwise) |
| `appsettings.json` | `LocalStorage` section added |

**Classification:** Render-only for the local storage feature itself; the **conditional
registration pattern** in `Program.cs` is portable and useful as a dev fallback.
**Port recommendation:**
- DO port the conditional `IFileStorageService` registration logic from `Program.cs`
  (keeps prod using Azure; gives developers a no-config local path).
- DO port `LocalStorageOptions`, `LocalFileStorageService`, and `FilesController`
  (harmless in prod; needed for local dev without Azure credentials).
- DO add `LocalStorage` section to `appsettings.Development.json` only (not
  `appsettings.json` / prod config).
- DO NOT port the SQLite-specific config or `EnsureCreatedAsync` path.

---

### Feature 11 – .NET Unit / Integration Test Suite

**What it does:** Adds 7 test classes covering security contracts, visibility
hardening, extension logic, notification frontend behavior, and portability
migration contracts. Zero tests existed in prod before this.

**New files (sandbox-only, all in `LeadManagementPortal.Tests/`):**

| File | Coverage |
|---|---|
| `CustomerAccessAndUpdateTests.cs` | Customer `GetAccessibleByIdAsync` and `UpdateAsync` access control |
| `CustomerVisibilityHardeningTests.cs` | Customer search visibility by role |
| `FrontendNotificationScriptTests.cs` | Verifies notification JS API contracts (string checks) |
| `LeadExtensionTests.cs` | `GrantExtension` role/rule behavior |
| `LeadsControllerSecurityContractsTests.cs` | Role-based action gating on lead endpoints |
| `PortabilityMigrationContractsTests.cs` | Confirms migration names exist and are discoverable |
| `SalesOrgAdminVisibilityTests.cs` | `SalesOrgAdmin` sees correct org-scoped data |
| `SeedingTool.cs` | Shared test DB seeding helper |

**Classification:** Portable (strongly recommended — port these before anything else)
**Port action:** Copy test files as-is. Run `dotnet test` in work repo to confirm
green before other PR steps.

---

### Feature 12 – Playwright Browser Test Suite

**What it does:** End-to-end browser tests using Playwright covering public pages,
authenticated flows, API surface parity, mobile layout, accessibility advisory,
and visual captures. Targets the deployed app URL via environment variable.

**New files (sandbox-only):**

| Path | Contents |
|---|---|
| `tests/browser/` | Full Playwright project (package.json, playwright.config.js, helpers/, specs/) |
| `tests/browser/specs/accessibility-advisory.spec.js` | WCAG advisory checks |
| `tests/browser/specs/api-surface.spec.js` | API endpoint availability |
| `tests/browser/specs/browser-capabilities.spec.js` | Cross-browser compat checks |
| `tests/browser/specs/mobile-layout.spec.js` | Responsive/mobile viewport |
| `tests/browser/specs/render-authenticated.spec.js` | Authenticated page rendering |
| `tests/browser/specs/render-public.spec.js` | Public page rendering |
| `tests/browser/specs/visual-capture.spec.js` | Screenshot capture for visual comparison |

**Classification:** Portable (strongly recommended)
**Port action:** Copy `tests/browser/` directory. Update `playwright.config.js`
`baseURL` to point to the work repo's staging URL. Run `npx playwright test` to
confirm parity.

---

### Feature 13 – GitHub Actions CI Workflows

**What it does:** Adds comprehensive CI coverage: .NET build/test, browser
parity tests, frontend JS guards, CodeQL security scanning, dependency review,
and Docker build validation.

**New/modified files:**

| File | Classification |
|---|---|
| `.github/workflows/ci.yml` | **Portable** (enhanced .NET CI) |
| `.github/workflows/browser-parity.yml` | **Portable** (Playwright cross-browser CI) |
| `.github/workflows/browser-quality-advisory.yml` | **Portable** (quality advisory CI) |
| `.github/workflows/frontend-guards.yml` | **Portable** (frontend JS linting/guards) |
| `.github/workflows/codeql.yml` | **Portable** (CodeQL security scan) |
| `.github/workflows/dependency-review.yml` | **Portable** (dependency review on PRs) |
| `.github/workflows/docker-build.yml` | **Render-only** (Render-specific Docker build; do not port) |
| `.github/actions/dotnet-ci/action.yml` | **Portable** (reusable composite action) |
| `.github/dependabot.yml` | **Portable** (automated dependency updates config) |

**Port action:**
- Port all workflows except `docker-build.yml`.
- Review `ci.yml` to confirm it does not reference any Render-specific env vars before
  adding to work repo.

---

### Feature 14 – NuGet Package: CsvHelper

**What it does:** Required by the Lead CSV Export feature (Feature 3).

| Change | Detail |
|---|---|
| `LeadManagementPortal.csproj` | Added `CsvHelper 33.0.1` |

**Note:** The git log shows several Dependabot bumps for EF Core / Identity / dotnet-ef
packages. Those bumps were applied as commits but **the `.csproj` in sandbox still
shows EF Core 8.0.0**. The Dependabot commits updated `.github/dependabot.yml` only,
not the actual package references. No EF Core version bump needs to be ported.

**Classification:** Portable
**Port action:** Add `<PackageReference Include="CsvHelper" Version="33.0.1" />` to
the work repo `.csproj`.

---

## Items That Are Render-Only (Never Port)

| Item | Reason |
|---|---|
| `render.yaml` | Render deploy config |
| `Dockerfile` | Render-specific container build |
| `.github/workflows/docker-build.yml` | Render-specific CI |
| `Program.cs` – SQLite provider branch | Free-tier SQLite; prod uses SQL Server |
| `Program.cs` – `EnsureCreatedAsync()` path | Only for SQLite; prod uses `MigrateAsync()` |
| `Program.cs` – fail-fast re-throw on seed error | Can be ported but review first; changes startup behavior |
| `Program.cs` – `ForwardedHeadersOptions` | Useful for any reverse-proxy deploy; evaluate if prod needs it |
| `Data/SeedData.cs` – demo user additions | Demo users; not for prod |
| `appsettings.json` – SQLite connection string | Dev/Render-only |
| `LeadManagementPortal/.config/AGENTS.md` etc. | Agent scaffolding docs; not needed in prod |

---

## Safe Port Workflow (Per Feature)

1. Read the relevant entry above; identify all files in scope.
2. In the work repo, create a feature branch:
   `git checkout -b feature/<name>`
3. Port candidate files manually (no bulk copy of directories).
4. If the feature adds EF entities: **regenerate migrations in the work repo**:
   `dotnet ef migrations add <Name> --project LeadManagementPortal/LeadManagementPortal.csproj`
5. Verify build + tests:
   `dotnet build && dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj`
6. Open PR with explicit "Not included" section listing all excluded Render-only files.

## Working Agreement

Every new feature commit in this sandbox should get an entry here on the same day
it lands. Use this log together with `git log --oneline` and a fresh
`MIGRATION_PLAYBOOK.md` compatibility audit before opening any work-repo PR.
