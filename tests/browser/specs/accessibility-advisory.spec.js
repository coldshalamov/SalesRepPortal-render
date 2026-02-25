const { test, expect } = require("@playwright/test");
const AxeBuilder = require("@axe-core/playwright").default;
const { loginAsAdmin } = require("../helpers/auth");

function criticalViolations(results) {
  return results.violations.filter((violation) => violation.impact === "critical");
}

test.describe("Accessibility Advisory @advisory", () => {
  test("@advisory login page has no critical axe violations", async ({ page }) => {
    await page.goto("/Account/Login");
    const scan = await new AxeBuilder({ page }).analyze();
    const critical = criticalViolations(scan);
    expect(
      critical,
      `Critical accessibility violations:\n${JSON.stringify(critical, null, 2)}`
    ).toEqual([]);
  });

  test("@advisory dashboard has no critical axe violations", async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto("/Dashboard");
    const scan = await new AxeBuilder({ page }).analyze();
    const critical = criticalViolations(scan);
    expect(
      critical,
      `Critical accessibility violations:\n${JSON.stringify(critical, null, 2)}`
    ).toEqual([]);
  });
});
