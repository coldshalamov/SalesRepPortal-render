const { test, expect } = require("@playwright/test");
const { loginAsAdmin } = require("../helpers/auth");
const {
  attachRuntimeCollectors,
  assertNoBlockingRuntimeIssues,
} = require("../helpers/runtime-health");

test.describe("Frontend API Surface", () => {
  test("search API returns expected shape for authenticated requests", async ({
    page,
  }) => {
    const runtime = attachRuntimeCollectors(page);
    await loginAsAdmin(page);

    const response = await page.request.get("/api/search?q=de");
    expect(response.status()).toBe(200);

    const payload = await response.json();
    expect(Array.isArray(payload.leads)).toBe(true);
    expect(Array.isArray(payload.customers)).toBe(true);

    await assertNoBlockingRuntimeIssues(runtime, "search api");
  });

  test("notifications API endpoints respond with expected contracts", async ({
    page,
  }) => {
    const runtime = attachRuntimeCollectors(page);
    await loginAsAdmin(page);

    const listRes = await page.request.get(
      "/api/notifications/get_notifications?limit=10&include_unread_count=true"
    );
    expect(listRes.status()).toBe(200);
    const listPayload = await listRes.json();
    expect(listPayload.success).toBe(true);
    expect(Array.isArray(listPayload.data.notifications)).toBe(true);
    expect(typeof listPayload.data.unread_count).toBe("number");

    const markAllRes = await page.request.post("/api/notifications/mark_all_read");
    expect(markAllRes.status()).toBe(200);
    const markAllPayload = await markAllRes.json();
    expect(markAllPayload.success).toBe(true);

    await assertNoBlockingRuntimeIssues(runtime, "notifications api");
  });
});
