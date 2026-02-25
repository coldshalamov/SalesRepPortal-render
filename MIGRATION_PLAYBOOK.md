# Migration Playbook (Sandbox -> Work Repo)

This file defines the exact process for moving selected feature changes from `SalesRepPortal-render` into `SalesRepPortal` safely.

## Latest Compatibility Audit (2026-02-25)

- Compared repos:
  - Sandbox: `D:\GitHub\SalesRepPortal-render`
  - Work snapshot: `D:\GitHub\SalesRepPortal-main.zip` (last write: 2026-02-24 10:41 PM local)
- Result summary:
  - The sandbox is a superset of the work snapshot (no files missing from sandbox).
  - `119` files that exist in both repos have different content.
  - `107` of those drifts are under `LeadManagementPortal/`.
- Practical meaning:
  - Do not port by broad merge or by copying whole folders.
  - Port as tightly scoped, feature-by-feature PRs only.

## Portability Risk Score (1-10)

- Bulk "port everything changed in sandbox": **9/10 (High danger)**
  - Main risks: behavior drift, role-permission drift, startup/provider drift, and schema mismatch.
- Scoped feature PRs with prechecks in this playbook: **4/10 (Manageable)**
  - Risk becomes acceptable only when schema + role + provider checks are explicitly completed.

## Full-Feature Port Is Feasible

Porting all product features to the work repo is achievable. The unsafe part is "bulk copy without structure," not the features themselves.

- Expected risk with the guided integration path below: **3-4/10**
- Expected risk with one-shot unmanaged copy: **9/10**
- Translation:
  - Yes, you can deliver a single clean PR to the work repo.
  - You should build that PR through a controlled integration branch with hard gates.

## Current High-Risk Items Found

1. **EF schema mismatch for follow-up tasks**
   - `LeadFollowUpTask` is used in models/services/controllers, but there is no SQL Server migration for it in sandbox.
   - Risk: runtime failure in work/prod when follow-up code hits a missing table.
   - Mitigation: create and validate a dedicated SQL Server migration in the work repo before feature rollout.

2. **Feature coupling with notifications**
   - `LeadsController` now depends on `INotificationService`, and sandbox includes notification model/service/migration.
   - Work snapshot has no notification stack.
   - Risk: compile/runtime break if lead changes are ported without notification prerequisites.
   - Mitigation: either:
     - PR A: port notifications end-to-end first, then pipeline PR, or
     - remove notification coupling from pipeline PR and defer notifications.

3. **Startup/provider/storage drift**
   - Sandbox `Program.cs` adds provider switching (`SqlServer`/`Sqlite`), `EnsureCreated()` path for SQLite, local-file storage fallback, forwarded headers, and fail-fast seeding.
   - Risk: production behavior changes if this is ported unreviewed (especially storage fallback behavior).
   - Mitigation: treat startup/storage changes as separate infra PR with explicit production acceptance criteria.

4. **Authorization and behavior drift in lead workflows**
   - Lead reassign/extension/edit flows differ from work snapshot.
   - Risk: unintended privilege broadening or business-rule regressions.
   - Mitigation: isolate role/permission changes into their own PR with reviewer sign-off and tests.

## Repository Boundaries

- `SalesRepPortal-render`: sandbox for experiments, Render deploy, free-tier compatibility work.
- `SalesRepPortal`: work repo tied to your organization workflow and production deployment path.
- Rule: no direct merge from sandbox repo into work repo.

## Never Port These Categories

- `render.yaml`, `Dockerfile`, `.render/*`, `RENDER.md`
- SQLite-only startup behavior and env-path tweaks used only for Render free tier
- Demo-account seeding / convenience-access logic
- Any environment-specific secrets or local-only operational shortcuts

## Candidate-to-Port Categories

- UI changes requested by stakeholders
- Matching controller/service logic required for those UI changes
- Small model/viewmodel updates needed by those features

## Port Procedure Per Feature

1. In sandbox repo, identify the exact feature commit(s):
   - `git log --oneline`
2. List changed files:
   - `git diff --name-only <base-commit>..<feature-commit>`
3. Classify files:
   - Product files -> candidate
   - Deploy/env/seed/provider files -> exclude
4. In work repo, create feature branch:
   - `git checkout -b feature/<name>`
5. Port only candidate file diffs manually or by patch.
6. Verify in work repo against SQL Server behavior:
   - `dotnet run --project LeadManagementPortal`
   - `dotnet test`
7. Open PR with explicit "Excluded from port" list.

## PR Template Notes

Include two sections in each PR:

- Included:
  - list each file ported and why
- Excluded (sandbox-only):
  - list each omitted Render or environment change

## Current Baseline Decision

- Render-only operational commits stay in sandbox.
- Product feature commits are ported in narrow PR slices only, never as bulk sync.

## Required PR Slicing (for current sandbox state)

1. `PR-0 (safety)`:
   - Add/verify SQL Server migration for `LeadFollowUpTask` in work repo.
   - Validate apply + rollback on staging clone.
2. `PR-1 (optional prerequisite)`:
   - Notifications foundation (model/service/controller/UI/migration), if lead changes depend on it.
3. `PR-2 (pipeline UX + API)`:
   - Lead board/table toggle, status movement endpoints, follow-up task endpoints, JS/CSS/view changes.
   - Keep this PR free from unrelated startup/provider/storage edits.
4. `PR-3 (role/rule changes)`:
   - Any access-control or business-rule adjustments (reassign/edit/extension) isolated for focused review.

## Single-PR Integration Route (All Features, Clean Merge)

Use this when leadership wants one PR in the work repo, but you still need safety.

1. In the work repo, create a dedicated integration branch:
   - `git checkout -b integration/feature-parity-20260225`
2. Port in this exact sequence (commit each step on the integration branch):
   - `Step A`: Schema prerequisites (`AddNotifications`, `AddLeadFollowUpTasks`) + model snapshot parity
   - `Step B`: Service/model contracts (interfaces, service implementations, model updates)
   - `Step C`: Controller/API behavior (lead pipeline actions, follow-up actions, notifications API)
   - `Step D`: Views/assets (pipeline JS/CSS, dashboard/login/layout UI refreshes)
   - `Step E`: Tests + CI guardrails for portability
3. After each step, run verification:
   - `dotnet restore`
   - `dotnet build`
   - `dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj`
   - `pwsh ./scripts/ci/check-portability-guardrails.ps1` (or `./scripts/ci/check-portability-guardrails.ps1` in PowerShell)
4. When branch is fully green, open **one PR** from `integration/feature-parity-20260225` to work repo `main`.
5. In the PR body, include:
   - "Port order used" (A-E above)
   - migration apply/rollback evidence
   - explicit excluded Render-only files list
   - smoke-test checklist results

This produces a single PR artifact while still getting the safety of staged integration internally.

## Recommended Commit Layout Inside The Single Integration PR

Keep commits grouped so reviewers can reason about risk:

1. `feat(db): add notifications and follow-up-task schema`
2. `feat(services): add notification/follow-up service contracts`
3. `feat(leads): add pipeline status/follow-up endpoints`
4. `feat(ui): add leads pipeline board assets and view wiring`
5. `feat(ui): dashboard/login/layout refresh`
6. `test(ci): add portability guardrails and contract tests`

If you need to rollback before merge, you can revert only the high-risk commit groups (schema/role changes) without losing all UI work.

## Hard Gates Before Opening The Single PR

All must pass:

1. SQL Server migration validation on staging clone:
   - apply up
   - execute smoke queries
   - verify down/rollback path
2. App + tests green on work repo branch.
3. No mixed Render-only files in the PR.
4. Role/permission regression check completed for:
   - `GrantExtension`
   - lead reassignment
   - lead status movement/convert flow
5. Documentation updated:
   - `MIGRATION_PLAYBOOK.md`
   - `PORTING_LOG.md`

## Must-Exclude From Product PRs Unless Explicitly Approved

- `render.yaml`, `Dockerfile`, `.render/*`, Render workflow tweaks
- SQLite runtime toggles intended only for Render free tier operations
- Any demo-seeding behavior or convenience test accounts
- Environment-specific keys/secrets/defaults that are not production-approved

## DB-change safety checklist (required for parity features)

When a feature adds EF entities/tables/columns (like pipeline follow-up tasks):

1. Treat schema changes as a separate PR step in the work repo.
2. Generate migration in the work repo (do not hand-copy generated files from sandbox):
   - `dotnet ef migrations add <Name> --project LeadManagementPortal/LeadManagementPortal.csproj`
3. Review migration for destructive operations:
   - Allowed by default: `CreateTable`, `AddColumn`, `CreateIndex`
   - Block by default: `DropTable`, `DropColumn`, column type narrowing, destructive data updates
4. Validate migration against a production-like SQL Server backup clone before merge.
5. Verify rollback path (down migration or backup restore plan) before production apply.
6. Roll out application code + migration together and monitor startup logs for migration/apply errors.

If any step fails, stop porting and keep the feature sandbox-only until fixed.

## CI Guardrails For Portability

- Always-on CI guard:
  - `scripts/ci/check-portability-guardrails.ps1`
  - Enforces:
    - migration metadata completeness (discoverable EF migrations),
    - follow-up/notification schema dependency contracts,
    - no mixed PRs that combine Render-only files with product code.
- Optional deep dry run against a real target zip:
  - `scripts/ci/portability-target-dry-run.ps1 -TargetRepoZipPath <path-to-zip>`
  - Applies portable files onto extracted target snapshot, then runs restore/build/tests there.
  - In GitHub Actions, this is exposed via manual dispatch input `target_zip_url`.
