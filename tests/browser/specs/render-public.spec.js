const { test, expect } = require("@playwright/test");
const { ADMIN_EMAIL, ADMIN_PASSWORD } = require("../helpers/auth");
const {
  attachRuntimeCollectors,
  assertNoBlockingRuntimeIssues,
} = require("../helpers/runtime-health");

test.describe("Public Surface Render", () => {
  test("login page renders and accepts credentials input", async ({ page }) => {
    const runtime = attachRuntimeCollectors(page);

    const response = await page.goto("/Account/Login");
    expect(response && response.status()).toBeLessThan(400);

    await expect(page).toHaveTitle(/login/i);
    await expect(page.getByLabel(/email/i)).toBeVisible();
    await expect(page.getByLabel(/password/i)).toBeVisible();
    await expect(page.getByRole("button", { name: /sign in/i })).toBeVisible();

    await page.getByLabel(/email/i).fill(ADMIN_EMAIL);
    await page.getByLabel(/password/i).fill(ADMIN_PASSWORD);
    await expect(page.getByLabel(/email/i)).toHaveValue(ADMIN_EMAIL);

    await assertNoBlockingRuntimeIssues(runtime, "login page");
  });

  test("login transition page is reachable and redirects to dashboard", async ({
    page,
  }) => {
    const runtime = attachRuntimeCollectors(page);

    await page.goto("/Account/Login");
    await page.getByLabel(/email/i).fill(ADMIN_EMAIL);
    await page.getByLabel(/password/i).fill(ADMIN_PASSWORD);
    await page.getByRole("button", { name: /sign in/i }).click();

    await page.waitForURL(/\/Account\/LoginTransition|\/Dashboard/, {
      timeout: 30_000,
    });

    if (page.url().includes("/Account/LoginTransition")) {
      await expect(page.locator("#transition-container")).toBeVisible();
      await expect(page.locator("#particle-canvas")).toBeVisible();
      await page.waitForURL(/\/Dashboard/, { timeout: 30_000 });
    }

    await expect(page).toHaveURL(/\/Dashboard/);
    await assertNoBlockingRuntimeIssues(runtime, "login transition");
  });
});
