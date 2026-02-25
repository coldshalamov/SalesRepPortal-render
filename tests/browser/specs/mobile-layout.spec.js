const { test, expect } = require("@playwright/test");
const { loginAsAdmin } = require("../helpers/auth");
const {
  attachRuntimeCollectors,
  assertNoBlockingRuntimeIssues,
} = require("../helpers/runtime-health");

function isMobileProject(testInfo) {
  return testInfo.project.name === "mobile-chrome" || testInfo.project.name === "mobile-safari";
}

test.describe("Mobile Layout and Interaction", () => {
  test("mobile login page has no horizontal overflow", async ({ page }, testInfo) => {
    test.skip(!isMobileProject(testInfo), "Mobile-only assertion");
    const runtime = attachRuntimeCollectors(page);

    await page.goto("/Account/Login");
    await expect(page.getByRole("button", { name: /sign in/i })).toBeVisible();

    const hasHorizontalOverflow = await page.evaluate(() => {
      return document.documentElement.scrollWidth > window.innerWidth + 2;
    });

    expect(hasHorizontalOverflow).toBe(false);
    await assertNoBlockingRuntimeIssues(runtime, "mobile login");
  });

  test("mobile navbar expands and key controls remain interactive", async ({
    page,
  }, testInfo) => {
    test.skip(!isMobileProject(testInfo), "Mobile-only assertion");
    const runtime = attachRuntimeCollectors(page);
    await loginAsAdmin(page);
    await page.goto("/Dashboard");

    const toggler = page.locator(".navbar-toggler");
    if (await toggler.isVisible()) {
      await toggler.click();
      await expect(page.locator(".navbar-collapse")).toHaveClass(/show/);
    }

    const notifButton = page.locator("#notifBellBtn");
    await expect(notifButton).toBeVisible();
    await notifButton.click();
    await expect(page.locator("#notifDropdown")).toHaveClass(/active/);

    await assertNoBlockingRuntimeIssues(runtime, "mobile nav interactions");
  });
});
