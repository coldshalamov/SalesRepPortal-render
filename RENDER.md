# Render.com deployment (Docker)

This repo includes a `Dockerfile` and `render.yaml` so you can deploy via a Render Blueprint.

## What the Blueprint does
- Deploys the ASP.NET Core app as a Docker web service.
- Uses SQLite on a persistent disk mounted at `/var/data` (good for demos/testing; not ideal for scale).
- Stores uploaded lead documents on the same disk (local storage).

## Deploy steps
1. Create a new GitHub repo and push this folder to it.
2. In Render: **New** → **Blueprint** → select your repo.
3. After the service is created, set a non-default admin password in Render **Environment**:
   - `SeedAdmin__Password` = (pick a strong password)
   - Optional: `SeedAdmin__Email` (defaults to `admin@dirxhealth.com`)
4. Deploy, then log in at `/Account/Login`.

## Demo logins (seeded)
When `SeedDemoUsers__Enabled=true` (already set in `render.yaml`), these accounts are created on first boot:
- Org admin: `orgadmin@demo.local`
- Group admin: `groupadmin@demo.local`
- Sales org admin: `salesorgadmin@demo.local`
- Sales rep: `salesrep@demo.local`

Password:
- Set `SeedDemoUsers__Password` in Render to whatever you want, or it defaults to `Demo123`.

## Optional env vars (if you want them enabled)
- SmartyStreets:
  - `SmartyStreets__SmartyStreetKey`
  - `SmartyStreets__Referer` (set to your Render URL)
- Azure Blob documents (if you prefer Azure instead of local disk):
  - `AzureStorage__ConnectionString` (and optionally `AzureStorage__ContainerName`)
