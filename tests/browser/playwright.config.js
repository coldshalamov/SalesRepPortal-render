const path = require("node:path");
const { defineConfig, devices } = require("@playwright/test");

const port = Number(process.env.PLAYWRIGHT_PORT || 5073);
const baseURL = process.env.BASE_URL || `http://127.0.0.1:${port}`;
const tmpRoot = path.resolve(__dirname, ".tmp");
const sqliteDbPath = path.join(tmpRoot, "leadportal-browser-tests.db");
const uploadsPath = path.join(tmpRoot, "uploads");
const appProjectPath = path.resolve(
  __dirname,
  "..",
  "..",
  "LeadManagementPortal",
  "LeadManagementPortal.csproj"
);

module.exports = defineConfig({
  testDir: path.join(__dirname, "specs"),
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 1 : undefined,
  timeout: 75_000,
  expect: {
    timeout: 12_000,
  },
  reporter: [
    ["list"],
    [
      "html",
      { outputFolder: path.join(__dirname, "playwright-report"), open: "never" },
    ],
    [
      "junit",
      { outputFile: path.join(__dirname, "test-results", "junit.xml") },
    ],
  ],
  outputDir: path.join(__dirname, "test-results"),
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
    actionTimeout: 15_000,
    navigationTimeout: 45_000,
  },
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
    {
      name: "firefox",
      use: { ...devices["Desktop Firefox"] },
    },
    {
      name: "webkit",
      use: { ...devices["Desktop Safari"] },
    },
    {
      name: "edge",
      use: {
        browserName: "chromium",
        channel: "msedge",
        viewport: { width: 1440, height: 900 },
      },
    },
    {
      name: "mobile-chrome",
      use: { ...devices["Pixel 7"] },
    },
    {
      name: "mobile-safari",
      use: { ...devices["iPhone 13"] },
    },
  ],
  globalSetup: path.join(__dirname, "global-setup.js"),
  webServer: process.env.SKIP_WEBSERVER
    ? undefined
    : {
        command: `dotnet run --project "${appProjectPath}" --urls "${baseURL}"`,
        url: `${baseURL}/Account/Login`,
        timeout: 240_000,
        reuseExistingServer: !process.env.CI,
        env: {
          ASPNETCORE_ENVIRONMENT: "Development",
          DatabaseProvider: "Sqlite",
          ConnectionStrings__DefaultConnection: `Data Source=${sqliteDbPath}`,
          SeedAdmin__Email: process.env.E2E_ADMIN_EMAIL || "admin@dirxhealth.com",
          SeedAdmin__Password: process.env.E2E_ADMIN_PASSWORD || "Admin@123",
          LocalStorage__RootPath: uploadsPath,
          SEED_DEMO_DATA: process.env.SEED_DEMO_DATA || "true",
        },
      },
});
