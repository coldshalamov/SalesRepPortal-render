#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";

const surfaceName = String(process.env.SURFACE_NAME || "surface").trim();
const browserName = String(process.env.BROWSER || "chromium").trim().toLowerCase();
const surfaceUrl = String(process.env.SURFACE_URL || "").trim();
const requiredSelectors = String(process.env.REQUIRED_SELECTORS || "body")
  .split("||")
  .map((x) => x.trim())
  .filter(Boolean);
const timeoutMs = Number(process.env.SURFACE_TIMEOUT_MS || "30000");
const settleMs = Number(process.env.SURFACE_SETTLE_MS || "2500");
const minBodyTextLength = Number(process.env.MIN_BODY_TEXT_LENGTH || "80");
const screenshotDir = String(process.env.SCREENSHOT_DIR || "tests/browser/playwright-report/surfaces");

if (!surfaceUrl) {
  console.error("[browser-surface-smoke] SURFACE_URL is required.");
  process.exit(2);
}

const playwright = await import("playwright");

function browserLauncher(name) {
  switch (name) {
    case "chromium":
      return playwright.chromium;
    case "firefox":
      return playwright.firefox;
    case "webkit":
      return playwright.webkit;
    case "edge":
      return playwright.chromium;
    default:
      throw new Error(`Unsupported browser: ${name}`);
  }
}

const launchOptions = browserName === "edge"
  ? { headless: true, channel: "msedge" }
  : { headless: true };

const browser = await browserLauncher(browserName).launch(launchOptions);
const context = await browser.newContext();
const page = await context.newPage();

const consoleErrors = [];
const requestFailures = [];

page.on("console", (msg) => {
  if (msg.type() === "error") {
    consoleErrors.push(msg.text());
  }
});

page.on("requestfailed", (req) => {
  requestFailures.push({
    url: req.url(),
    method: req.method(),
    resourceType: req.resourceType(),
    errorText: req.failure()?.errorText || "unknown",
  });
});

let response;
let fatalError = null;

try {
  response = await page.goto(surfaceUrl, {
    waitUntil: "domcontentloaded",
    timeout: timeoutMs,
  });
  await page.waitForTimeout(settleMs);
} catch (error) {
  fatalError = error instanceof Error ? error.message : String(error);
}

const safeSurface = surfaceName.replace(/[^a-z0-9-_]/gi, "-");
fs.mkdirSync(screenshotDir, { recursive: true });
const screenshotPath = path.join(screenshotDir, `${safeSurface}-${browserName}.png`);
if (!fatalError) {
  await page.screenshot({ path: screenshotPath, fullPage: true }).catch(() => {});
}

const selectorResults = [];
if (!fatalError) {
  for (const selector of requiredSelectors) {
    const locator = page.locator(selector);
    const count = await locator.count();
    const visible = count > 0 ? await locator.first().isVisible().catch(() => false) : false;
    selectorResults.push({ selector, count, visible, ok: count > 0 && visible });
  }
}

const bodyTextLength = fatalError
  ? 0
  : await page.evaluate(() => {
      return (document.body?.innerText || "").replace(/\s+/g, " ").trim().length;
    });

const nonAssetFailures = requestFailures.filter((item) => {
  return !["image", "font", "media"].includes(item.resourceType);
});

const failedSelectors = selectorResults.filter((item) => !item.ok);
const httpStatus = Number(response?.status?.() || 0);

const ok = !fatalError &&
  httpStatus >= 200 &&
  httpStatus < 400 &&
  failedSelectors.length === 0 &&
  bodyTextLength >= minBodyTextLength &&
  nonAssetFailures.length === 0 &&
  consoleErrors.length === 0;

const summary = {
  ok,
  surfaceName,
  browserName,
  surfaceUrl,
  httpStatus,
  fatalError,
  minBodyTextLength,
  bodyTextLength,
  selectorResults,
  failedSelectors,
  requestFailures: nonAssetFailures,
  consoleErrors,
  screenshotPath,
};

console.log(JSON.stringify(summary, null, 2));

await browser.close();
process.exit(ok ? 0 : 1);
