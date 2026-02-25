const { expect } = require("@playwright/test");

const IGNORED_CONSOLE_PATTERNS = [
  /favicon\.ico/i,
];

const IGNORED_REQUEST_ERROR_PATTERNS = [
  /ERR_ABORTED/i,
  /NS_BINDING_ABORTED/i,
  /aborted/i,
];

function attachRuntimeCollectors(page) {
  const state = {
    pageErrors: [],
    consoleErrors: [],
    requestFailures: [],
  };

  page.on("pageerror", (error) => {
    state.pageErrors.push(String(error && error.message ? error.message : error));
  });

  page.on("console", (msg) => {
    if (msg.type() !== "error") return;
    const text = msg.text();
    const location = msg.location ? msg.location() : {};
    state.consoleErrors.push({
      text: String(text || ""),
      url: String(location && location.url ? location.url : ""),
    });
  });

  page.on("requestfailed", (request) => {
    state.requestFailures.push({
      url: request.url(),
      method: request.method(),
      resourceType: request.resourceType(),
      errorText: request.failure() ? request.failure().errorText : "unknown",
    });
  });

  return state;
}

function isIgnorableConsoleError(item) {
  return IGNORED_CONSOLE_PATTERNS.some((pattern) => {
    return pattern.test(item.text) || pattern.test(item.url);
  });
}

function isIgnorableRequestFailure(item) {
  if (["image", "font", "media"].includes(item.resourceType)) {
    return true;
  }

  const text = `${item.url} ${item.errorText}`.trim();
  return IGNORED_REQUEST_ERROR_PATTERNS.some((pattern) => pattern.test(text));
}

function getBlockingIssues(state) {
  return {
    pageErrors: state.pageErrors.slice(),
    consoleErrors: state.consoleErrors.filter((item) => !isIgnorableConsoleError(item)),
    requestFailures: state.requestFailures.filter((item) => !isIgnorableRequestFailure(item)),
  };
}

function formatIssues(issues) {
  return JSON.stringify(issues, null, 2);
}

async function assertNoBlockingRuntimeIssues(state, context) {
  const issues = getBlockingIssues(state);
  const contextLabel = context ? ` (${context})` : "";

  expect(
    issues.pageErrors,
    `Unhandled runtime exceptions detected${contextLabel}:\n${formatIssues(issues)}`
  ).toEqual([]);

  expect(
    issues.consoleErrors,
    `Console errors detected${contextLabel}:\n${formatIssues(issues)}`
  ).toEqual([]);

  expect(
    issues.requestFailures,
    `Critical request failures detected${contextLabel}:\n${formatIssues(issues)}`
  ).toEqual([]);
}

module.exports = {
  attachRuntimeCollectors,
  assertNoBlockingRuntimeIssues,
};
