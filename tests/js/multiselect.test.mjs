import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { JSDOM } from "jsdom";

const scriptPath = path.resolve(process.cwd(), "../../LeadManagementPortal/wwwroot/js/multiselect.js");
const scriptSource = fs.readFileSync(scriptPath, "utf8");

function buildFixture() {
  return `
    <!doctype html>
    <html>
      <body>
        <form>
          <select id="products" name="productIds" multiple data-enhance="multiselect">
            <option value="a">Alpha</option>
            <option value="b">Beta</option>
          </select>
        </form>
      </body>
    </html>
  `;
}

async function setupDom(html = buildFixture()) {
  const dom = new JSDOM(html, {
    runScripts: "outside-only",
    url: "http://localhost/"
  });

  const { window } = dom;
  window.eval(scriptSource);

  window.document.dispatchEvent(new window.Event("DOMContentLoaded", { bubbles: true }));
  await Promise.resolve();

  return { dom, window, document: window.document };
}

test("clicking custom option dispatches native change on underlying select", async () => {
  const { document } = await setupDom();
  const select = document.getElementById("products");
  const display = document.querySelector(".multi-select-display");

  let changeCount = 0;
  select.addEventListener("change", () => {
    changeCount += 1;
  });

  display.click();
  const firstItem = document.querySelector(".multi-select-item");
  firstItem.click();

  assert.equal(select.options[0].selected, true);
  assert.equal(changeCount, 1);
});

test("space key toggles focused option and dispatches native change", async () => {
  const { document, window } = await setupDom();
  const select = document.getElementById("products");
  const display = document.querySelector(".multi-select-display");

  let changeCount = 0;
  select.addEventListener("change", () => {
    changeCount += 1;
  });

  display.click();
  const firstItem = document.querySelector(".multi-select-item");
  firstItem.focus();

  firstItem.dispatchEvent(new window.KeyboardEvent("keydown", {
    key: " ",
    code: "Space",
    bubbles: true
  }));

  assert.equal(select.options[0].selected, true);
  assert.equal(changeCount, 1);
  assert.equal(firstItem.getAttribute("aria-selected"), "true");
});

test("arrow keys move focus between options in the listbox", async () => {
  const { document, window } = await setupDom();
  const display = document.querySelector(".multi-select-display");

  display.click();
  const items = document.querySelectorAll(".multi-select-item");
  const firstItem = items[0];
  const secondItem = items[1];

  firstItem.focus();
  firstItem.dispatchEvent(new window.KeyboardEvent("keydown", {
    key: "ArrowDown",
    code: "ArrowDown",
    bubbles: true
  }));

  assert.equal(document.activeElement, secondItem);
  assert.equal(firstItem.tabIndex, -1);
  assert.equal(secondItem.tabIndex, 0);
});

test("init API can enhance dynamically injected multiselect controls", async () => {
  const { document, window } = await setupDom(`
    <!doctype html>
    <html><body><div id="host"></div></body></html>
  `);

  assert.ok(window.DiRxMultiSelect, "Expected DiRxMultiSelect to be available on window");
  assert.equal(typeof window.DiRxMultiSelect.init, "function");

  const host = document.getElementById("host");
  host.innerHTML = `
    <select id="dynamic" multiple data-enhance="multiselect">
      <option value="x">Xray</option>
    </select>
  `;

  window.DiRxMultiSelect.init();

  const dynamicSelect = document.getElementById("dynamic");
  const wrapper = dynamicSelect.closest(".multi-select-wrapper");
  assert.ok(wrapper, "Expected dynamic multiselect to be enhanced by init()");
});
