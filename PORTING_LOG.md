# Porting Log (Render Sandbox -> Work Repo)

Purpose: track every change made in `SalesRepPortal-render`, classify whether it should ever be ported to the work repo, and define a safe PR workflow.

## Rules

1. Never port `Render-only` commits to the work repo.
2. Port only selected files/commits to a dedicated feature branch in the work repo.
3. Keep infrastructure changes (Render, SQLite, demo seeding) out of work PRs unless explicitly requested.

## Current Commit Log

### `af9e0eb` - Fix Render SQLite initialization and fail fast on seed errors

- Scope: `LeadManagementPortal/Program.cs`, `LeadManagementPortal/Data/SeedData.cs`
- Reason: Render free tier uses SQLite and hit startup/login failure.
- Classification: **Render-only**
- Port to work repo: **No** (work production uses SQL Server + migrations).

### `51ad6e4` - Switch Render blueprint to free tier (no disk)

- Scope: `render.yaml`, `RENDER.md`
- Classification: **Render-only**
- Port to work repo: **No**

### `d4c9be2` - Seed demo users for each role

- Scope: `LeadManagementPortal/Data/SeedData.cs`, `render.yaml`, `RENDER.md`
- Classification: **Render-only by default**
- Port to work repo: **No**, unless business explicitly wants demo-account seeding behavior.

### `d117b9f` - Initial Render-ready snapshot

- Scope: baseline clone + Render deployment setup files.
- Classification: **Mixed baseline**
- Port to work repo: **No direct porting**; this is a sandbox repo baseline.

## Safe Port Workflow

1. Build feature only in app files relevant to requested UI/backend behavior.
2. Before porting, run:
   - `git log --oneline` in `SalesRepPortal-render`
   - `git diff --name-only <base>..<feature-commit>`
3. Classify each changed file:
   - Product behavior/UI -> candidate to port
   - Deploy/seeding/env/render/sqlite -> do not port
4. In the work repo, create a branch and port only candidate changes.
5. Validate locally against SQL Server-oriented behavior before PR.
6. Open PR with explicit "Not included" section (Render-only deltas excluded).

## Working Agreement For Future Changes

For every new change, this file should get a new entry:

- Commit hash
- Files changed
- Why the change exists
- Classification (`Portable`, `Portable with edits`, `Render-only`)
- Porting notes
