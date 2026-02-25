const fs = require("node:fs");
const { test, expect } = require("@playwright/test");
const { loginAsAdmin } = require("../helpers/auth");

async function captureAndAssert(page, testInfo, name) {
  const outputPath = testInfo.outputPath(`${name}.png`);
  await page.screenshot({ path: outputPath, fullPage: true });
  const stat = fs.statSync(outputPath);
  expect(stat.size, `Screenshot was empty for ${name}`).toBeGreaterThan(1024);
  await testInfo.attach(name, {
    path: outputPath,
    contentType: "image/png",
  });
}

test.describe("Visual Capture Smoke", () => {
  test("captures login page screenshot", async ({ page }, testInfo) => {
    await page.goto("/Account/Login");
    await captureAndAssert(page, testInfo, "login-page");
  });

  test("captures dashboard screenshot for authenticated user", async ({
    page,
  }, testInfo) => {
    await loginAsAdmin(page);
    await page.goto("/Dashboard");
    await captureAndAssert(page, testInfo, "dashboard");
  });
});
