const { test, expect } = require("@playwright/test");
const { loginAsAdmin } = require("../helpers/auth");
const {
  attachRuntimeCollectors,
  assertNoBlockingRuntimeIssues,
} = require("../helpers/runtime-health");

const ADMIN_ROUTES = [
  { path: "/Dashboard", name: "Dashboard" },
  { path: "/Leads", name: "Leads" },
  { path: "/Customers", name: "Customers" },
  { path: "/Commissions", name: "Commissions" },
  { path: "/SalesGroups", name: "Groups" },
  { path: "/SalesOrgs", name: "Orgs" },
  { path: "/Users", name: "Users" },
  { path: "/Settings", name: "Settings" },
  { path: "/Account/ChangePassword", name: "Change Password" },
];

test.describe("Authenticated Render Smoke", () => {
  async function ensureNavbarLinksVisible(page, linkToReveal) {
    if (await linkToReveal.isVisible()) return;

    const toggler = page.locator("nav button.navbar-toggler");
    if (await toggler.isVisible()) {
      await toggler.click();
      await expect(linkToReveal).toBeVisible();
    }
  }

  test("organization admin can render core portal routes without client errors", async ({
    page,
  }) => {
    const runtime = attachRuntimeCollectors(page);
    await loginAsAdmin(page);

    for (const route of ADMIN_ROUTES) {
      const response = await page.goto(route.path);
      expect(response && response.status(), `HTTP status for ${route.path}`).toBeLessThan(400);
      await expect(page.locator("main")).toBeVisible();
      await expect(page.locator("body")).toBeVisible();
    }

    await assertNoBlockingRuntimeIssues(runtime, "authenticated routes");
  });

  test("navbar links are present and route successfully", async ({ page }) => {
    const runtime = attachRuntimeCollectors(page);
    await loginAsAdmin(page);

    const links = page.locator("nav .navbar-nav a.nav-link");
    const count = await links.count();
    expect(count).toBeGreaterThan(6);

    for (let i = 0; i < count; i++) {
      const link = links.nth(i);
      const href = await link.getAttribute("href");
      if (!href || href === "#") continue;

      await ensureNavbarLinksVisible(page, link);
      await link.click();
      await page.waitForLoadState("domcontentloaded");
      expect(page.url(), `clicked nav link href=${href}`).toContain(href.split("?")[0]);
    }

    await assertNoBlockingRuntimeIssues(runtime, "navbar navigation");
  });
});
