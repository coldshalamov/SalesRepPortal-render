import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { JSDOM } from "jsdom";

const scriptPath = path.resolve(process.cwd(), "../../LeadManagementPortal/wwwroot/js/leads-pipeline.js");
const scriptSource = fs.readFileSync(scriptPath, "utf8");

function buildFixture({ seed = [], config = {} } = {}) {
  const seedJson = JSON.stringify(seed);
  const configJson = JSON.stringify({
    updateStatusUrl: "/Leads/UpdateStatus",
    addFollowUpUrl: "/Leads/AddFollowUp",
    completeFollowUpUrl: "/Leads/CompleteFollowUp",
    deleteFollowUpsUrl: "/Leads/DeleteFollowUps",
    canConvert: true,
    ...config
  });

  return `
    <!doctype html>
    <html>
      <body>
        <form id="pipelineCsrfTokenForm">
          <input name="__RequestVerificationToken" value="token" />
        </form>

        <div id="pipelineWorkspace">
          <div id="pipelineBoardView">
            <div id="pipelineColumns"></div>
          </div>
        </div>

        <div id="leadsTableView" class="d-none"></div>
        <button id="pipelineViewBtn" type="button">Pipeline</button>
        <button id="tableViewBtn" type="button">Table</button>

        <span id="pipelineStatTotal"></span>
        <span id="pipelineStatActive"></span>
        <span id="pipelineStatConversion"></span>
        <span id="pipelineStatUrgent"></span>

        <div id="pipelineLeadModal">
          <h5 id="pipelineLeadModalTitle"></h5>
          <a id="pipelineLeadDetailsLink" href="#"></a>
          <a id="pipelineLeadEditLink" href="#"></a>

          <div id="pipelineDetailName"></div>
          <div id="pipelineDetailCompany"></div>
          <div id="pipelineDetailEmail"></div>
          <div id="pipelineDetailPhone"></div>
          <div id="pipelineDetailRep"></div>
          <div id="pipelineDetailOrg"></div>
          <div id="pipelineDetailNotes"></div>

          <div id="pipelineStageButtons"></div>

          <button type="button" data-quick-task="call_tomorrow" id="quickCallTomorrow">Call Tomorrow</button>

          <button type="button" id="pipelineAddTaskBtn">Add Task</button>
          <button type="button" id="pipelineDeleteTasksBtn" class="d-none">Delete Selected</button>
          <div id="pipelineTaskFormCard" class="d-none">
            <select id="pipelineTaskType">
              <option value="call">Call</option>
            </select>
            <input id="pipelineTaskDueDate" type="date" />
            <input id="pipelineTaskDescription" type="text" />
            <button type="button" id="pipelineTaskSaveBtn">Save</button>
            <button type="button" id="pipelineTaskCancelBtn">Cancel</button>
          </div>

          <div id="pipelineTasksList"></div>
        </div>

        <script id="leadsPipelineSeed" type="application/json">${seedJson}</script>
        <script id="leadsPipelineConfig" type="application/json">${configJson}</script>
      </body>
    </html>
  `;
}

async function setupDom({ seed, config } = {}) {
  const dom = new JSDOM(buildFixture({ seed, config }), {
    runScripts: "outside-only",
    url: "http://localhost/"
  });

  const { window } = dom;

  // Stub fetch for tests that exercise follow-up task saves.
  window.fetch = async () => ({
    ok: true,
    json: async () => ({ success: true, tasks: [], message: "ok" })
  });

  // The pipeline script uses Bootstrap's Modal helper in production.
  // Provide a minimal stub so JSDOM tests don't trip over a missing global.
  window.bootstrap = {
    Modal: {
      getOrCreateInstance() {
        return { show() {}, hide() {} };
      }
    }
  };

  window.eval(scriptSource);
  await Promise.resolve();
  return { dom, window, document: window.document };
}

test("dragstart handler does not throw when dataTransfer is missing", async () => {
  const { document, window } = await setupDom({
    seed: [{ id: "lead-1", status: "New", company: "Acme", firstName: "A", lastName: "B", tasks: [] }]
  });

  const card = document.querySelector("[data-lead-id='lead-1']");
  assert.ok(card, "Expected a rendered pipeline card for lead-1");

  assert.doesNotThrow(() => {
    card.dispatchEvent(new window.Event("dragstart", { bubbles: true }));
  });
});

test("Expired leads are not silently remapped into the New column", async () => {
  const { document } = await setupDom({
    seed: [
      { id: "lead-new", status: "New", company: "NewCo", firstName: "N", lastName: "Ew", tasks: [] },
      { id: "lead-expired", status: "Expired", isExpired: true, company: "OldCo", firstName: "O", lastName: "Ld", tasks: [] }
    ]
  });

  const board = document.getElementById("pipelineColumns");
  assert.ok(board);

  assert.ok(board.innerHTML.includes("data-lead-id=\"lead-new\""), "Expected New lead to be present on the board");
  assert.equal(board.innerHTML.includes("data-lead-id=\"lead-expired\""), false, "Expected Expired lead to not render into pipeline columns");
});

test("quick-task autosave does not double-post when clicked twice quickly", async () => {
  let fetchCalls = 0;
  let resolveFetch;

  const fetchPromise = new Promise((resolve) => {
    resolveFetch = () =>
      resolve({
        ok: true,
        json: async () => ({ success: true, tasks: [], message: "ok" })
      });
  });

  const { document, window } = await setupDom({
    seed: [{ id: "lead-1", status: "New", company: "Acme", firstName: "A", lastName: "B", tasks: [] }]
  });

  window.fetch = () => {
    fetchCalls += 1;
    return fetchPromise;
  };

  // Select the lead so quick tasks are eligible.
  const card = document.querySelector("[data-lead-id='lead-1']");
  card.dispatchEvent(new window.MouseEvent("click", { bubbles: true }));

  // Click the quick task twice before the first request resolves.
  const quick = document.getElementById("quickCallTomorrow");
  quick.dispatchEvent(new window.MouseEvent("click", { bubbles: true }));
  quick.dispatchEvent(new window.MouseEvent("click", { bubbles: true }));

  assert.equal(fetchCalls, 1);

  resolveFetch();
  await Promise.resolve();
});
