const fs = require("node:fs");
const path = require("node:path");

module.exports = async function globalSetup() {
  const tmpRoot = path.resolve(__dirname, ".tmp");
  const uploadsRoot = path.join(tmpRoot, "uploads");
  const dbPath = path.join(tmpRoot, "leadportal-browser-tests.db");
  const dbSidecars = [`${dbPath}-shm`, `${dbPath}-wal`];

  fs.mkdirSync(tmpRoot, { recursive: true });
  fs.mkdirSync(uploadsRoot, { recursive: true });

  if (fs.existsSync(dbPath)) {
    fs.unlinkSync(dbPath);
  }
  for (const sidecar of dbSidecars) {
    if (fs.existsSync(sidecar)) {
      fs.unlinkSync(sidecar);
    }
  }
};
