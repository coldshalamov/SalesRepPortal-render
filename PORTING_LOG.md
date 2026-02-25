# Porting Log (Render Sandbox -> Work Repo)

Purpose: track every change made in `SalesRepPortal-render`, classify whether it should ever be ported to the work repo, and define a safe PR workflow.

## Rules

1. Never port `Render-only` commits to the work repo.
2. Port only selected files/commits to a dedicated feature branch in the work repo.
3. Keep infrastructure changes (Render, SQLite, demo seeding) out of work PRs unless explicitly requested.

## Edit Ledger Rules

For every new commit in this sandbox repo, append an entry with:

- Commit hash
- Files changed
- Why the change exists
- Classification (`Portable`, `Portable with edits`, `Render-only`)
- Exact port action (`Port`, `Port with edits`, `Do not port`)

## Current Commit Log

### `AUDIT-2026-02-25` - Compatibility scan vs `D:\GitHub\SalesRepPortal-main.zip`

- Scope:
  - Full repo comparison between:
    - sandbox: `D:\GitHub\SalesRepPortal-render`
    - work snapshot zip: `D:\GitHub\SalesRepPortal-main.zip` (2026-02-24)
- Findings:
  - No files in the work snapshot are missing from sandbox.
  - `119` shared-path files differ by content (`107` under `LeadManagementPortal/`).
  - This confirms heavy drift and requires narrow PR slicing.
- Classification: **Process gate (required)**
- Exact port action: **Use `MIGRATION_PLAYBOOK.md` PR slicing + risk controls before any product PR**

### `0502010` - Dashboard + layout + navigation overhaul

- Scope (representative):
  - `LeadManagementPortal/Views/Dashboard/Index.cshtml`
  - `LeadManagementPortal/Views/Shared/_Layout.cshtml`
  - `LeadManagementPortal/wwwroot/css/site.css`
  - `LeadManagementPortal/Controllers/SalesGroupsController.cs` (related UX support)
- Reason:
  - Large UI/UX redesign and dashboard behavior changes.
- Classification: **Portable with edits**
- Exact port action:
  - Port in dedicated UI PR(s), excluding unrelated startup/provider/seed changes.

### `c0a83e3` (+ related files still present at HEAD) - Follow-up task and lead pipeline foundation

- Scope:
  - `LeadManagementPortal/Models/LeadFollowUpTask.cs`
  - `LeadManagementPortal/Models/Lead.cs`
  - `LeadManagementPortal/Data/ApplicationDbContext.cs`
  - `LeadManagementPortal/Services/ILeadService.cs`
  - `LeadManagementPortal/Services/LeadService.cs`
  - `LeadManagementPortal/Controllers/LeadsController.cs`
  - `LeadManagementPortal/Views/Leads/Index.cshtml`
  - `LeadManagementPortal/wwwroot/js/leads-pipeline.js`
  - `LeadManagementPortal/wwwroot/css/leads-pipeline.css`
- Reason:
  - Add Kanban pipeline UX, status movement API, and follow-up task management.
- Classification: **Portable with edits (high attention)**
- Exact port action:
  1. In work repo, generate and review SQL Server migration for `LeadFollowUpTask` before enabling endpoints.
  2. Port lead pipeline changes without accidental coupling to unrelated features.
  3. Add/adjust tests for role-based access and follow-up task lifecycle.

### `6c6dca4` (+ related files) - Customer visibility hardening and service-layer changes

- Scope (representative):
  - `LeadManagementPortal/Services/CustomerService.cs`
  - `LeadManagementPortal/Services/ICustomerService.cs`
  - `LeadManagementPortal/Controllers/CustomersController.cs`
  - `LeadManagementPortal/Views/Customers/*`
  - `LeadManagementPortal.Tests/CustomerVisibilityHardeningTests.cs`
- Reason:
  - Tighten customer visibility and search behavior.
- Classification: **Portable with edits**
- Exact port action:
  - Port as a separate behavior PR with tests included.

### `Render/deploy surface` - sandbox-only runtime and infra deltas

- Scope:
  - `render.yaml`, `Dockerfile`, `.render/*`, `RENDER.md`
  - SQLite/Render bootstrap-specific toggles in startup/config
- Reason:
  - Keep sandbox deployable on Render free tier.
- Classification: **Render-only**
- Exact port action: **Do not port**

### `Notification stack` - currently coupled with lead workflows in sandbox

- Scope:
  - `LeadManagementPortal/Models/Notification.cs`
  - `LeadManagementPortal/Services/INotificationService.cs`
  - `LeadManagementPortal/Services/NotificationService.cs`
  - `LeadManagementPortal/Controllers/NotificationsApiController.cs`
  - `LeadManagementPortal/Migrations/20260224_AddNotifications.cs`
  - notification UI assets
- Reason:
  - User-facing notification feature now referenced by lead flows.
- Classification: **Portable with edits**
- Exact port action:
  - Port as prerequisite PR or remove dependency from lead PR.

## Safe Port Workflow

1. Build feature only in app files relevant to requested UI/backend behavior.
2. Run portability guardrails:
   - `scripts/ci/check-portability-guardrails.ps1`
   - Optional target simulation: `scripts/ci/portability-target-dry-run.ps1 -TargetRepoZipPath <zip>`
3. Before porting, run:
   - `git log --oneline` in `SalesRepPortal-render`
   - `git diff --name-only <base>..<feature-commit>`
4. Classify each changed file:
   - Product behavior/UI -> candidate to port
   - Deploy/seeding/env/render/sqlite -> do not port
5. In the work repo, create a branch and port only candidate changes.
6. Validate locally against SQL Server-oriented behavior before PR.
7. Open PR with explicit "Not included" section (Render-only deltas excluded).

## Working Agreement For Future Changes

Every commit entry must be appended on the same day it is created.

## Important Note

Older entries from prior sandbox history may no longer represent current `main` branch contents. Use this file together with `git log --oneline` and the latest compatibility audit before each port PR.
