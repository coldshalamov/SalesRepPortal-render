(function () {
  "use strict";

  const seedEl = document.getElementById("leadsPipelineSeed");
  const configEl = document.getElementById("leadsPipelineConfig");
  if (!seedEl || !configEl) {
    return;
  }

  const boardRoot = document.getElementById("pipelineColumns");
  const boardView = document.getElementById("pipelineBoardView");
  const tableView = document.getElementById("leadsTableView");
  const pipelineBtn = document.getElementById("pipelineViewBtn");
  const tableBtn = document.getElementById("tableViewBtn");
  const statTotal = document.getElementById("pipelineStatTotal");
  const statActive = document.getElementById("pipelineStatActive");
  const statConversion = document.getElementById("pipelineStatConversion");
  const statUrgent = document.getElementById("pipelineStatUrgent");
  const csrfToken = document.querySelector("#pipelineCsrfTokenForm input[name='__RequestVerificationToken']")?.value ?? "";
  const pipelineWorkspace = document.getElementById("pipelineWorkspace");

  let seed = [];
  let config = { updateStatusUrl: "", canConvert: false };

  try {
    seed = JSON.parse(seedEl.textContent || "[]");
    config = JSON.parse(configEl.textContent || "{}");
  } catch (error) {
    console.error("Unable to parse pipeline data:", error);
    return;
  }

  const columnStatuses = ["New", "Contacted", "Qualified", "Proposal", "Negotiation", "Lost"];
  const activeStatuses = ["New", "Contacted", "Qualified", "Proposal", "Negotiation"];
  const stageLabels = {
    New: "New Lead",
    Contacted: "Contacted",
    Qualified: "Qualified",
    Proposal: "Proposal",
    Negotiation: "Negotiation",
    Converted: "Converted",
    Lost: "Lost"
  };

  const state = {
    leads: Array.isArray(seed) ? seed.slice() : [],
    selectedLeadId: null,
    dragLeadId: null
  };

  function escapeHtml(value) {
    return String(value ?? "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  function normalizeStatus(rawStatus) {
    const status = String(rawStatus || "").trim();
    if (statusLabels()[status]) {
      return status;
    }
    return status;
  }

  function statusLabels() {
    return stageLabels;
  }

  function getLeadById(leadId) {
    return state.leads.find((lead) => String(lead.id) === String(leadId)) || null;
  }

  function urgencyClass(urgency) {
    const normalized = String(urgency || "").toLowerCase();
    if (normalized === "critical") return "pipeline-urgency-critical";
    if (normalized === "high") return "pipeline-urgency-high";
    if (normalized === "medium") return "pipeline-urgency-medium";
    return "pipeline-urgency-low";
  }

  function updateStats() {
    const total = state.leads.length;
    const active = state.leads.filter((lead) => activeStatuses.includes(normalizeStatus(lead.status))).length;
    const converted = state.leads.filter((lead) => normalizeStatus(lead.status) === "Converted").length;
    const lost = state.leads.filter((lead) => normalizeStatus(lead.status) === "Lost").length;
    const conversionBase = converted + lost;
    const conversionRate = conversionBase > 0 ? ((converted / conversionBase) * 100).toFixed(1) : "0.0";
    const urgent = state.leads.filter((lead) => {
      const status = normalizeStatus(lead.status);
      if (status === "Converted" || status === "Lost") return false;
      const urgency = String(lead.urgency || "").toLowerCase();
      return urgency === "critical" || urgency === "high";
    }).length;

    if (statTotal) statTotal.textContent = String(total);
    if (statActive) statActive.textContent = String(active);
    if (statConversion) statConversion.textContent = `${conversionRate}%`;
    if (statUrgent) statUrgent.textContent = String(urgent);
  }

  function renderBoard() {
    if (!boardRoot) return;

    const columnsHtml = columnStatuses.map((status) => {
      const leads = state.leads.filter((lead) => normalizeStatus(lead.status) === status);
      const cards = leads.map((lead) => {
        const fullName = [lead.firstName, lead.lastName].filter(Boolean).join(" ").trim() || "No contact";
        const daysLabel = lead.isExpired ? "Expired" : `${Math.max(0, Number(lead.daysRemaining || 0))} days`;
        return `
          <article class="pipeline-card" draggable="true" data-lead-id="${escapeHtml(lead.id)}">
            <div class="pipeline-card-top">
              <div>
                <div class="pipeline-card-company">${escapeHtml(lead.company || "-")}</div>
                <div class="pipeline-card-name">${escapeHtml(fullName)}</div>
              </div>
              <div class="pipeline-card-days">${escapeHtml(daysLabel)}</div>
            </div>
            <div class="pipeline-card-meta">
              <div class="pipeline-meta-row">
                <span class="pipeline-meta-label">Rep</span>
                <span class="pipeline-meta-value">${escapeHtml(lead.assignedRep || "-")}</span>
              </div>
              <div class="pipeline-meta-row">
                <span class="pipeline-meta-label">Urgency</span>
                <span class="pipeline-urgency-badge ${urgencyClass(lead.urgency)}">${escapeHtml(lead.urgency || "Low")}</span>
              </div>
            </div>
          </article>
        `;
      }).join("");

      return `
        <section class="pipeline-column" data-status="${status}">
          <header class="pipeline-column-header">
            <span class="pipeline-column-title">${escapeHtml(statusLabels()[status])}</span>
            <span class="pipeline-column-count">${leads.length}</span>
          </header>
          <div class="pipeline-column-body">
            ${cards || `<div class="pipeline-empty-column">No leads</div>`}
          </div>
        </section>
      `;
    }).join("");

    boardRoot.innerHTML = columnsHtml;
  }

  function showToast(message, variant) {
    if (!pipelineWorkspace) return;

    const color = variant === "success" ? "success" : variant === "danger" ? "danger" : "info";
    const alert = document.createElement("div");
    alert.className = `alert alert-${color} py-2 px-3 mt-2`;
    alert.setAttribute("role", "alert");
    alert.textContent = message;
    pipelineWorkspace.insertAdjacentElement("afterend", alert);
    setTimeout(() => alert.remove(), 2600);
  }

  async function updateLeadStatus(leadId, targetStatus, closeModalAfter) {
    if (!config.updateStatusUrl) {
      showToast("Pipeline endpoint is not configured.", "danger");
      return;
    }

    const lead = getLeadById(leadId);
    if (!lead) return;

    const originalStatus = normalizeStatus(lead.status);
    if (originalStatus === targetStatus) {
      return;
    }

    try {
      const response = await fetch(config.updateStatusUrl, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "RequestVerificationToken": csrfToken
        },
        body: JSON.stringify({
          leadId: String(leadId),
          status: targetStatus
        }),
        credentials: "same-origin"
      });

      if (!response.ok) {
        const payload = await response.json().catch(() => ({}));
        throw new Error(payload.message || "Unable to move lead.");
      }

      const payload = await response.json().catch(() => ({}));
      if (!payload.success) {
        throw new Error(payload.message || "Unable to move lead.");
      }

      lead.status = targetStatus;
      if (targetStatus === "Converted" || targetStatus === "Lost") {
        lead.daysRemaining = 0;
      }
      renderBoard();
      updateStats();
      refreshModalIfOpen();
      showToast(payload.message || "Lead moved successfully.", "success");

      if (closeModalAfter) {
        const modalEl = document.getElementById("pipelineLeadModal");
        const modal = modalEl ? bootstrap.Modal.getInstance(modalEl) : null;
        modal?.hide();
      }
    } catch (error) {
      showToast(error?.message || "Unable to move lead right now.", "danger");
    }
  }

  function setView(mode) {
    const isPipeline = mode === "pipeline";
    if (boardView) boardView.classList.toggle("d-none", !isPipeline);
    if (tableView) tableView.classList.toggle("d-none", isPipeline);

    if (pipelineBtn) {
      pipelineBtn.classList.toggle("btn-primary", isPipeline);
      pipelineBtn.classList.toggle("btn-outline-primary", !isPipeline);
    }
    if (tableBtn) {
      tableBtn.classList.toggle("btn-primary", !isPipeline);
      tableBtn.classList.toggle("btn-outline-primary", isPipeline);
    }
  }

  function renderModalStageButtons(lead) {
    const container = document.getElementById("pipelineStageButtons");
    if (!container || !lead) return;

    const statuses = columnStatuses.slice();
    if (config.canConvert) {
      statuses.push("Converted");
    }

    container.innerHTML = statuses.map((status) => {
      const isCurrent = normalizeStatus(lead.status) === status;
      const styleClass = isCurrent ? "btn btn-primary btn-sm" : "btn btn-outline-secondary btn-sm";
      return `<button type="button" class="${styleClass}" data-target-status="${status}" data-pipeline-stage-btn="1">${escapeHtml(statusLabels()[status])}</button>`;
    }).join("");
  }

  function openLeadModal(leadId) {
    const lead = getLeadById(leadId);
    if (!lead) return;

    state.selectedLeadId = String(lead.id);

    const modalTitle = document.getElementById("pipelineLeadModalTitle");
    const detailsLink = document.getElementById("pipelineLeadDetailsLink");
    const editLink = document.getElementById("pipelineLeadEditLink");

    if (modalTitle) modalTitle.textContent = lead.company || "Lead";
    if (detailsLink) detailsLink.setAttribute("href", lead.detailsUrl || "#");
    if (editLink) {
      if (lead.editUrl) {
        editLink.classList.remove("d-none");
        editLink.setAttribute("href", lead.editUrl);
      } else {
        editLink.classList.add("d-none");
      }
    }

    const fullName = [lead.firstName, lead.lastName].filter(Boolean).join(" ").trim() || "No contact";
    const setText = (id, value) => {
      const element = document.getElementById(id);
      if (element) element.textContent = value;
    };

    setText("pipelineDetailName", fullName);
    setText("pipelineDetailCompany", lead.company || "-");
    setText("pipelineDetailEmail", lead.email || "-");
    setText("pipelineDetailPhone", lead.phone || "-");
    setText("pipelineDetailRep", lead.assignedRep || "-");
    setText("pipelineDetailOrg", lead.salesOrg || "-");

    renderModalStageButtons(lead);

    const modalEl = document.getElementById("pipelineLeadModal");
    if (!modalEl) return;
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();
  }

  function refreshModalIfOpen() {
    if (!state.selectedLeadId) return;
    const modalEl = document.getElementById("pipelineLeadModal");
    if (!modalEl || !modalEl.classList.contains("show")) return;
    openLeadModal(state.selectedLeadId);
  }

  function bindEvents() {
    if (pipelineBtn) {
      pipelineBtn.addEventListener("click", () => setView("pipeline"));
    }
    if (tableBtn) {
      tableBtn.addEventListener("click", () => setView("table"));
    }

    if (!boardRoot) return;

    boardRoot.addEventListener("click", (event) => {
      const card = event.target.closest(".pipeline-card");
      if (!card) return;
      const leadId = card.getAttribute("data-lead-id");
      if (!leadId) return;
      openLeadModal(leadId);
    });

    boardRoot.addEventListener("dragstart", (event) => {
      const card = event.target.closest(".pipeline-card");
      if (!card) return;
      const leadId = card.getAttribute("data-lead-id");
      if (!leadId) return;
      state.dragLeadId = leadId;
      card.classList.add("is-dragging");
      event.dataTransfer?.setData("text/plain", leadId);
      event.dataTransfer.effectAllowed = "move";
    });

    boardRoot.addEventListener("dragend", (event) => {
      event.target.closest(".pipeline-card")?.classList.remove("is-dragging");
      boardRoot.querySelectorAll(".pipeline-column.is-drop-target").forEach((column) => {
        column.classList.remove("is-drop-target");
      });
      state.dragLeadId = null;
    });

    boardRoot.addEventListener("dragover", (event) => {
      const column = event.target.closest(".pipeline-column");
      if (!column) return;
      event.preventDefault();
      column.classList.add("is-drop-target");
      event.dataTransfer.dropEffect = "move";
    });

    boardRoot.addEventListener("dragleave", (event) => {
      const column = event.target.closest(".pipeline-column");
      if (!column) return;
      if (!column.contains(event.relatedTarget)) {
        column.classList.remove("is-drop-target");
      }
    });

    boardRoot.addEventListener("drop", async (event) => {
      const column = event.target.closest(".pipeline-column");
      if (!column) return;
      event.preventDefault();
      column.classList.remove("is-drop-target");

      const targetStatus = column.getAttribute("data-status");
      const leadId = event.dataTransfer?.getData("text/plain") || state.dragLeadId;
      if (!targetStatus || !leadId) return;
      await updateLeadStatus(leadId, targetStatus, false);
    });

    const modalEl = document.getElementById("pipelineLeadModal");
    if (modalEl) {
      modalEl.addEventListener("click", async (event) => {
        const stageButton = event.target.closest("[data-pipeline-stage-btn='1']");
        if (!stageButton || !state.selectedLeadId) return;
        const targetStatus = stageButton.getAttribute("data-target-status");
        if (!targetStatus) return;
        await updateLeadStatus(state.selectedLeadId, targetStatus, targetStatus === "Converted");
      });
    }
  }

  renderBoard();
  updateStats();
  bindEvents();
  setView("pipeline");
})();
