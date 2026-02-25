const { test, expect } = require("@playwright/test");

test.describe("Browser Capability Baseline", () => {
  test("required runtime APIs exist", async ({ page }) => {
    await page.goto("/Account/Login");

    const capabilities = await page.evaluate(() => {
      return {
        fetch: typeof window.fetch === "function",
        abortController: typeof window.AbortController === "function",
        localStorage: typeof window.localStorage !== "undefined",
        matchMedia: typeof window.matchMedia === "function",
        requestAnimationFrame: typeof window.requestAnimationFrame === "function",
        cssFocusVisible: typeof CSS !== "undefined" && CSS.supports("selector(:focus-visible)"),
      };
    });

    expect(capabilities.fetch).toBe(true);
    expect(capabilities.abortController).toBe(true);
    expect(capabilities.localStorage).toBe(true);
    expect(capabilities.matchMedia).toBe(true);
    expect(capabilities.requestAnimationFrame).toBe(true);
    expect(capabilities.cssFocusVisible).toBe(true);
  });
});
