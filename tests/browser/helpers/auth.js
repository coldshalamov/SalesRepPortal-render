const { expect } = require("@playwright/test");

const ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL || "admin@dirxhealth.com";
const ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD || "Admin@123";

async function loginAsAdmin(page) {
  await page.goto("/Account/Login");
  await page.getByLabel(/email/i).fill(ADMIN_EMAIL);
  await page.getByLabel(/password/i).fill(ADMIN_PASSWORD);

  await Promise.all([
    page.waitForLoadState("domcontentloaded"),
    page.getByRole("button", { name: /sign in/i }).click(),
  ]);

  await page.waitForURL(/\/Account\/LoginTransition|\/Dashboard/, {
    timeout: 25_000,
  });

  if (page.url().includes("/Account/LoginTransition")) {
    // The transition page should auto-redirect, but we don't block all
    // follow-on tests on that animation path.
    await page.goto("/Dashboard");
  }

  await expect(page).toHaveURL(/\/Dashboard/);
}

module.exports = {
  ADMIN_EMAIL,
  ADMIN_PASSWORD,
  loginAsAdmin,
};
