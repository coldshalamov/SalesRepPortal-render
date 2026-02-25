# Browser CI Suite

This package contains cross-browser and mobile render checks for `LeadManagementPortal`.

## What it covers

- Public page render health (login + login transition)
- Authenticated route smoke for organization-admin surface
- Frontend API contract smoke (`/api/search`, `/api/notifications`)
- Mobile layout and navbar interaction checks
- Runtime error tripwires (uncaught JS errors, console errors, failed critical requests)
- Browser capability baseline checks
- Accessibility audits (`@advisory`, axe-core)
- Visual capture evidence per run

## Local usage

From repo root:

```bash
npm --prefix tests/browser ci
npx --prefix tests/browser playwright install --with-deps
npm --prefix tests/browser run test:parity
```

Advisory-only checks:

```bash
npm --prefix tests/browser run test:advisory
```

## Test environment defaults

The Playwright config starts the ASP.NET app automatically with:

- `DatabaseProvider=Sqlite`
- `ConnectionStrings__DefaultConnection=tests/browser/.tmp/leadportal-browser-tests.db`
- `SeedAdmin__Email=admin@dirxhealth.com`
- `SeedAdmin__Password=Admin@123`
- `SEED_DEMO_DATA=true`

You can override via environment variables:

- `E2E_ADMIN_EMAIL`
- `E2E_ADMIN_PASSWORD`
- `BASE_URL` (to target an existing running environment)
- `SKIP_WEBSERVER=1` (if app is already running)
