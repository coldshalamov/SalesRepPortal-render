# Sandbox / Demo Deployment Guide

This repo is a sandbox worktree used for demos and feature testing on Render's free tier.
It ships with pre-seeded demo accounts so you can skip the setup ceremony and go straight
to adding leads.

---

## Demo Accounts

All accounts are seeded automatically on every startup when `SeedDemoData=true`.

| Role             | Email                   | Password        |
|------------------|-------------------------|-----------------|
| Org Admin        | admin@dirxhealth.com    | Admin@123       |
| Group Admin      | groupadmin@demo.com     | GroupAdmin@123  |
| Org Admin (org)  | orgadmin@demo.com       | OrgAdmin@123    |
| Sales Rep        | rep@demo.com            | SalesRep@123    |

**Pre-seeded structure**

```
SalesGroup: "Demo Group"
  └── SalesOrg: "Demo Org"
        ├── orgadmin@demo.com  (SalesOrgAdmin)
        └── rep@demo.com       (SalesRep)
  └── groupadmin@demo.com    (GroupAdmin)
```

To add a lead: log in as `rep@demo.com`, hit **New Lead**, and pick "Demo Rep" / "Demo Group" / "Demo Org" from the dropdowns — they're already there.

---

## Why data survives deploys

The seed runs inside `SeedData.Initialize()` which is called at every app startup
(`Program.cs:113`). Because it's idempotent (check-before-insert), re-running it on a
fresh DB after a Render container restart recreates all demo accounts without duplicates.

> **Note on the free-tier DB**: Render free web services store the SQLite file at
> `/tmp/leadportal.db`. The `/tmp` filesystem is ephemeral — it is wiped when the
> container restarts or sleeps. **This is expected and fine for the sandbox** because
> the seeder always rebuilds the baseline on the next boot. Any leads you add manually
> will be gone after a restart; that's the tradeoff of the free tier. There is no benefit
> to committing the `.db` file to Git — Render won't use it; it always creates the file
> fresh at `/tmp`.

---

## Keeping this from touching production

The demo seeding is guarded by a single environment variable:

```yaml
# render.yaml (this sandbox)
- key: SeedDemoData
  value: "true"
```

**For prod, simply do not set this variable (or set it to `"false"`).**
The seeder checks `configuration["SeedDemoData"]` and skips the entire block unless
the value is literally `"true"` (case-insensitive).

### Prod deployment checklist

1. **Do not copy `render.yaml` directly.** Create a separate `render-prod.yaml` (or
   configure env vars manually in the Render dashboard) and omit `SeedDemoData`.
2. Set `SeedAdmin:Email` and `SeedAdmin:Password` to real values via Render's
   secret environment variables — never commit them.
3. Use a persistent database (Render PostgreSQL add-on, or an external SQL Server)
   rather than the ephemeral `/tmp` SQLite path.
4. Set `ASPNETCORE_ENVIRONMENT=Production` (already the default in `render.yaml`).
5. Rotate all demo passwords above if this codebase is ever used as a base for prod.

---

## Switching to a persistent DB (when you're ready for prod)

Change these env vars in the Render dashboard:

```
DatabaseProvider=SqlServer   (or leave blank — defaults to SqlServer)
ConnectionStrings__DefaultConnection=Server=...;Database=LeadPortal;...
```

The app will run EF migrations automatically on startup (`MigrateAsync()`).
The SQLite path (`/tmp/leadportal.db`) is only used when `DatabaseProvider=Sqlite`.
