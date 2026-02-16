# Migration Playbook (Sandbox -> Work Repo)

This file defines the exact process for moving selected feature changes from `SalesRepPortal-render` into `SalesRepPortal` safely.

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
- Product feature commits will be ported one-by-one after your approval.
