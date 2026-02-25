(function () {
  "use strict";

  const seedEl = document.getElementById("leadsPipelineSeed");
  const configEl = document.getElementById("leadsPipelineConfig");
  if (!seedEl || !configEl) return;

  const boardRoot = document.getElementById("pipelineColumns");
  const boardView = document.getElementById("pipelineBoardView");
  const tableView = document.getElementById("leadsTableView");
  const pipelineBtn = document.getElementById("pipelineViewBtn");
  const tableBtn = document.getElementById("tableViewBtn");
  const statTotal = document.getElementById("pipelineStatTotal");
  const statActive = document.getElementById("pipelineStatActive");
  const statConversion = document.getElementById("pipelineStatConversion");
  const statOverdue = document.getElementById("pipelineStatUrgent");
  const pipelineWorkspace = document.getElementById("pipelineWorkspace");
  const csrfToken = document.querySelector("#pipelineCsrfTokenForm input[name='__RequestVerificationToken']")?.value ?? "";

  let seed = [];
  let config = {
    updateStatusUrl: "",
    addFollowUpUrl: "",
    completeFollowUpUrl: "",
    deleteFollowUpsUrl: "",
    canConvert: false
  };

  try {
    seed = JSON.parse(seedEl.textContent || "[]");
    config = JSON.parse(configEl.textContent || "{}");
  } catch (error) {
    console.error("Unable to parse pipeline payload.", error);
    return;
  }

  const columnStatuses = ["New", "Contacted", "Qualified", "Proposal", "Negotiation", "Converted", "Lost"];
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

  const canonicalStatusByLower = Object.create(null);
  columnStatuses.forEach((status) => {
    canonicalStatusByLower[status.toLowerCase()] = status;
  });
  const unknownStatuses = new Set();

  const quickTaskTemplates = {
    call_tomorrow: { type: "call", description: "Call tomorrow to review next step.", dueOffsetDays: 1, autoSave: true },
    send_proposal: { type: "email", description: "Send proposal with updated pricing.", dueOffsetDays: null, autoSave: false },
    schedule_demo: { type: "meeting", description: "Schedule product demonstration.", dueOffsetDays: null, autoSave: false },
    check_in_week: { type: "check-in", description: "Check in next week on decision timeline.", dueOffsetDays: 7, autoSave: true }
  };

  const state = {
    leads: Array.isArray(seed) ? seed.map(normalizeLead) : [],
    selectedLeadId: null,
    dragLeadId: null,
    isSavingTask: false
  };
  const viewStorageKey = "leadsPipelinePreferredView";

  function normalizeLead(lead) {
    const tasks = Array.isArray(lead.tasks) ? lead.tasks.map(normalizeTask) : [];
    return { ...lead, tasks };
  }

  function normalizeTask(task) {
    return {
      id: Number(task.id || 0),
      type: String(task.type || "call"),
      description: String(task.description || ""),
      dueDate: task.dueDate ? String(task.dueDate) : "",
      isCompleted: !!task.isCompleted,
      completedAt: task.completedAt ? String(task.completedAt) : ""
    };
  }

  function toIsoDate(dateObj) {
    const yyyy = dateObj.getFullYear();
    const mm = String(dateObj.getMonth() + 1).padStart(2, "0");
    const dd = String(dateObj.getDate()).padStart(2, "0");
    return `${yyyy}-${mm}-${dd}`;
  }

  function daysFromNow(days) {
    const date = new Date();
    date.setHours(12, 0, 0, 0);
    date.setDate(date.getDate() + Number(days || 0));
    return toIsoDate(date);
  }

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
    if (!status) return "New";

    const canonical = canonicalStatusByLower[status.toLowerCase()];
    if (canonical) return canonical;

    if (!unknownStatuses.has(status)) {
      unknownStatuses.add(status);
      console.warn("Leads pipeline received unexpected status:", status);
    }

    // Preserve unknown statuses so we don't silently remap them to "New".
    return status;
  }

  function urgencyClass(urgency) {
    const normalized = String(urgency || "").toLowerCase();
    if (normalized === "critical") return "pipeline-urgency-critical";
    if (normalized === "high") return "pipeline-urgency-high";
    if (normalized === "medium") return "pipeline-urgency-medium";
    return "pipeline-urgency-low";
  }

  function getLeadById(leadId) {
    return state.leads.find((lead) => String(lead.id) === String(leadId)) || null;
  }

  function getPendingTasks(lead) {
    return (lead.tasks || [])
      .filter((task) => !task.isCompleted)
      .sort((left, right) => {
        const leftDue = left.dueDate || "9999-12-31";
        const rightDue = right.dueDate || "9999-12-31";
        return leftDue.localeCompare(rightDue);
      });
  }

  function getOverdueCount(lead) {
    const today = toIsoDate(new Date());
    return getPendingTasks(lead).filter((task) => task.dueDate && task.dueDate < today).length;
  }

  function getGlobalOverdueCount() {
    const today = toIsoDate(new Date());
    let count = 0;
    state.leads.forEach((lead) => {
      (lead.tasks || []).forEach((task) => {
        if (!task.isCompleted && task.dueDate && task.dueDate < today) {
          count += 1;
        }
      });
    });
    return count;
  }

  function updateStats() {
    const total = state.leads.length;
    const active = state.leads.filter((lead) => activeStatuses.includes(normalizeStatus(lead.status))).length;
    const converted = state.leads.filter((lead) => normalizeStatus(lead.status) === "Converted").length;
    const lost = state.leads.filter((lead) => normalizeStatus(lead.status) === "Lost").length;
    const conversionBase = converted + lost;
    const conversionRate = conversionBase > 0 ? ((converted / conversionBase) * 100).toFixed(1) : "0.0";
    const overdue = getGlobalOverdueCount();

    if (statTotal) statTotal.textContent = String(total);
    if (statActive) statActive.textContent = String(active);
    if (statConversion) statConversion.textContent = `${conversionRate}%`;
    if (statOverdue) statOverdue.textContent = String(overdue);
  }

  function renderBoard() {
    if (!boardRoot) return;

    const html = columnStatuses.map((status) => {
      const leads = state.leads.filter((lead) => normalizeStatus(lead.status) === status);
      const cards = leads.map((lead) => {
        const fullName = [lead.firstName, lead.lastName].filter(Boolean).join(" ").trim() || "No contact";
        const normalizedStatus = normalizeStatus(lead.status);
        const daysLabel = lead.isExpired
          ? "Expired"
          : normalizedStatus === "Converted"
            ? "Converted"
            : `${Math.max(0, Number(lead.daysRemaining || 0))} days`;
        const pendingTasks = getPendingTasks(lead);
        const overdueCount = getOverdueCount(lead);
        const nextTask = pendingTasks[0];
        const taskSummary = nextTask
          ? `${nextTask.type.toUpperCase()}: ${nextTask.description.substring(0, 35)}${nextTask.description.length > 35 ? "..." : ""}`
          : "No open tasks";
        const notes = String(lead.notes || "").trim();
        const notesPreview = notes
          ? `${notes.substring(0, 60)}${notes.length > 60 ? "..." : ""}`
          : "";

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
              <div class="pipeline-meta-row">
                <span class="pipeline-meta-label">Tasks</span>
                <span class="pipeline-meta-value">${pendingTasks.length} open${overdueCount > 0 ? ` (${overdueCount} overdue)` : ""}</span>
              </div>
              <div class="pipeline-meta-row">
                <span class="pipeline-meta-label">Next</span>
                <span class="pipeline-meta-value">${escapeHtml(taskSummary)}</span>
              </div>
            </div>
            ${notesPreview ? `<div class="pipeline-card-notes">${escapeHtml(notesPreview)}</div>` : ""}
          </article>
        `;
      }).join("");

      return `
        <section class="pipeline-column" data-status="${status}">
          <header class="pipeline-column-header">
            <span class="pipeline-column-title">${escapeHtml(stageLabels[status])}</span>
            <span class="pipeline-column-count">${leads.length}</span>
          </header>
          <div class="pipeline-column-body">
            ${cards || `<div class="pipeline-empty-column">No leads</div>`}
          </div>
        </section>
      `;
    }).join("");

    boardRoot.innerHTML = html;
  }

  function showToast(message, variant) {
    if (!pipelineWorkspace) return;
    const alertColor = variant === "success" ? "success" : variant === "danger" ? "danger" : "info";
    const alert = document.createElement("div");
    alert.className = `alert alert-${alertColor} py-2 px-3 mt-2`;
    alert.setAttribute("role", "alert");
    alert.textContent = message;

    // Keep toast positioning stable even if surrounding layout changes.
    let toastHost = pipelineWorkspace.querySelector("[data-pipeline-toast-host='1']");
    if (!toastHost) {
      toastHost = document.createElement("div");
      toastHost.setAttribute("data-pipeline-toast-host", "1");
      toastHost.className = "px-3 pb-3";
      pipelineWorkspace.insertAdjacentElement("afterbegin", toastHost);
    }

    toastHost.appendChild(alert);
    setTimeout(() => alert.remove(), 2600);
  }

  async function postJson(url, payload) {
    const response = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "RequestVerificationToken": csrfToken
      },
      body: JSON.stringify(payload),
      credentials: "same-origin"
    });

    let body = {};
    try {
      body = await response.json();
    } catch {
      body = {};
    }

    if (!response.ok || body.success === false) {
      throw new Error(body.message || "Request failed.");
    }
    return body;
  }

  function applyReturnedTasks(leadId, tasks) {
    const lead = getLeadById(leadId);
    if (!lead) return;
    lead.tasks = Array.isArray(tasks) ? tasks.map(normalizeTask) : [];
  }

  async function updateLeadStatus(leadId, targetStatus, closeModalAfter) {
    if (!config.updateStatusUrl) {
      showToast("Pipeline endpoint is not configured.", "danger");
      return;
    }

    const lead = getLeadById(leadId);
    if (!lead) return;
    if (normalizeStatus(lead.status) === targetStatus) return;

    try {
      const result = await postJson(config.updateStatusUrl, { leadId: String(leadId), status: targetStatus });
      lead.status = targetStatus;
      if (targetStatus === "Converted" || targetStatus === "Lost") lead.daysRemaining = 0;
      renderBoard();
      updateStats();
      refreshModalIfOpen();
      showToast(result.message || "Lead moved successfully.", "success");

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

    try {
      localStorage.setItem(viewStorageKey, isPipeline ? "pipeline" : "table");
    } catch {
      // Ignore storage issues (privacy mode / blocked storage).
    }
  }

  function renderModalStageButtons(lead) {
    const container = document.getElementById("pipelineStageButtons");
    if (!container || !lead) return;
    const statuses = ["New", "Contacted", "Qualified", "Proposal", "Negotiation", "Lost", "Converted"];

    container.innerHTML = statuses.map((status) => {
      const isCurrent = normalizeStatus(lead.status) === status;
      const isConvertBlocked = status === "Converted" && !config.canConvert;
      const buttonClass = isCurrent ? "btn btn-primary btn-sm" : "btn btn-outline-secondary btn-sm";
      return `<button type="button" class="${buttonClass}" data-target-status="${status}" data-pipeline-stage-btn="1" ${isConvertBlocked ? "disabled" : ""}>${escapeHtml(stageLabels[status])}</button>`;
    }).join("");
  }

  function renderTasksList(lead) {
    const container = document.getElementById("pipelineTasksList");
    const deleteButton = document.getElementById("pipelineDeleteTasksBtn");
    if (!container || !lead) return;

    const tasks = (lead.tasks || []).slice().sort((left, right) => {
      if (left.isCompleted !== right.isCompleted) return left.isCompleted ? 1 : -1;
      const leftDue = left.dueDate || "9999-12-31";
      const rightDue = right.dueDate || "9999-12-31";
      return leftDue.localeCompare(rightDue);
    });

    if (!tasks.length) {
      container.innerHTML = `<div class="text-muted small border rounded p-3">No follow-up tasks yet.</div>`;
      if (deleteButton) deleteButton.classList.add("d-none");
      return;
    }

    const today = toIsoDate(new Date());
    const html = tasks.map((task) => {
      const overdue = !task.isCompleted && task.dueDate && task.dueDate < today;
      const dueLabel = task.dueDate ? (overdue ? `${task.dueDate} (Overdue)` : task.dueDate) : "No due date";
      return `
        <div class="d-flex align-items-start gap-2 border rounded p-2 mb-2 ${task.isCompleted ? "bg-light" : ""}">
          <input type="checkbox" class="form-check-input mt-1" data-task-select="1" value="${task.id}" />
          <div class="flex-grow-1">
            <div class="d-flex justify-content-between gap-2">
              <div class="${task.isCompleted ? "text-decoration-line-through text-muted" : ""}">${escapeHtml(task.description)}</div>
              <span class="badge ${overdue ? "bg-danger" : task.isCompleted ? "bg-secondary" : "bg-info text-dark"}">${escapeHtml(task.type.toUpperCase())}</span>
            </div>
            <div class="small ${overdue ? "text-danger" : "text-muted"}">Due: ${escapeHtml(dueLabel)}</div>
          </div>
          <button type="button" class="btn btn-sm btn-outline-success ${task.isCompleted ? "d-none" : ""}" data-task-complete="1" data-task-id="${task.id}">Done</button>
        </div>
      `;
    }).join("");

    container.innerHTML = html;
    if (deleteButton) deleteButton.classList.add("d-none");
  }

  function setTaskFormVisible(isVisible) {
    const card = document.getElementById("pipelineTaskFormCard");
    if (!card) return;
    card.classList.toggle("d-none", !isVisible);
  }

  function resetTaskForm() {
    const typeEl = document.getElementById("pipelineTaskType");
    const dueEl = document.getElementById("pipelineTaskDueDate");
    const descriptionEl = document.getElementById("pipelineTaskDescription");
    if (typeEl) typeEl.value = "call";
    if (dueEl) dueEl.value = "";
    if (descriptionEl) descriptionEl.value = "";
  }

  async function saveTaskFromForm() {
    if (!state.selectedLeadId || !config.addFollowUpUrl) return;
    if (state.isSavingTask) return;

    const typeEl = document.getElementById("pipelineTaskType");
    const dueEl = document.getElementById("pipelineTaskDueDate");
    const descriptionEl = document.getElementById("pipelineTaskDescription");
    const description = descriptionEl?.value?.trim() || "";
    if (!description) {
      showToast("Task description is required.", "danger");
      return;
    }

    state.isSavingTask = true;
    const saveButton = document.getElementById("pipelineTaskSaveBtn");
    if (saveButton) saveButton.setAttribute("disabled", "disabled");

    try {
      const result = await postJson(config.addFollowUpUrl, {
        leadId: state.selectedLeadId,
        type: typeEl?.value || "call",
        description,
        dueDate: dueEl?.value || null
      });
      applyReturnedTasks(state.selectedLeadId, result.tasks);
      renderBoard();
      renderTasksList(getLeadById(state.selectedLeadId));
      updateStats();
      resetTaskForm();
      setTaskFormVisible(false);
      showToast(result.message || "Task added.", "success");
    } catch (error) {
      showToast(error?.message || "Unable to add task.", "danger");
    } finally {
      state.isSavingTask = false;
      if (saveButton) saveButton.removeAttribute("disabled");
    }
  }

  async function completeTask(taskId) {
    if (!state.selectedLeadId || !config.completeFollowUpUrl) return;
    try {
      const result = await postJson(config.completeFollowUpUrl, {
        leadId: state.selectedLeadId,
        followUpId: Number(taskId)
      });
      applyReturnedTasks(state.selectedLeadId, result.tasks);
      renderBoard();
      renderTasksList(getLeadById(state.selectedLeadId));
      updateStats();
      showToast(result.message || "Task completed.", "success");
    } catch (error) {
      showToast(error?.message || "Unable to complete task.", "danger");
    }
  }

  async function deleteSelectedTasks() {
    if (!state.selectedLeadId || !config.deleteFollowUpsUrl) return;
    const selected = Array.from(document.querySelectorAll("#pipelineTasksList [data-task-select='1']:checked"))
      .map((element) => Number(element.value))
      .filter((id) => id > 0);

    if (!selected.length) return;
    const shouldDelete = window.confirm(`Delete ${selected.length} selected task(s)?`);
    if (!shouldDelete) return;

    try {
      const result = await postJson(config.deleteFollowUpsUrl, {
        leadId: state.selectedLeadId,
        followUpIds: selected
      });
      applyReturnedTasks(state.selectedLeadId, result.tasks);
      renderBoard();
      renderTasksList(getLeadById(state.selectedLeadId));
      updateStats();
      showToast(result.message || "Tasks deleted.", "success");
    } catch (error) {
      showToast(error?.message || "Unable to delete tasks.", "danger");
    }
  }

  async function applyQuickTask(templateKey) {
    const template = quickTaskTemplates[templateKey];
    if (!template || !state.selectedLeadId) return;

    const typeEl = document.getElementById("pipelineTaskType");
    const dueEl = document.getElementById("pipelineTaskDueDate");
    const descriptionEl = document.getElementById("pipelineTaskDescription");

    if (typeEl) typeEl.value = template.type;
    if (descriptionEl) descriptionEl.value = template.description;
    if (dueEl) dueEl.value = template.dueOffsetDays == null ? "" : daysFromNow(template.dueOffsetDays);
    setTaskFormVisible(true);

    if (template.autoSave) {
      await saveTaskFromForm();
    } else {
      showToast("Task template loaded. Click Save Task to confirm.", "info");
    }
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
    setText("pipelineDetailNotes", String(lead.notes || "").trim() || "-");

    renderModalStageButtons(lead);
    renderTasksList(lead);
    resetTaskForm();
    setTaskFormVisible(false);

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
    if (pipelineBtn) pipelineBtn.addEventListener("click", () => setView("pipeline"));
    if (tableBtn) tableBtn.addEventListener("click", () => setView("table"));
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
      if (event.dataTransfer) event.dataTransfer.effectAllowed = "move";
    });

    boardRoot.addEventListener("dragend", (event) => {
      event.target.closest(".pipeline-card")?.classList.remove("is-dragging");
      boardRoot.querySelectorAll(".pipeline-column.is-drop-target").forEach((column) => column.classList.remove("is-drop-target"));
      state.dragLeadId = null;
    });

    boardRoot.addEventListener("dragover", (event) => {
      const column = event.target.closest(".pipeline-column");
      if (!column) return;
      event.preventDefault();
      column.classList.add("is-drop-target");
      if (event.dataTransfer) event.dataTransfer.dropEffect = "move";
    });

    boardRoot.addEventListener("dragleave", (event) => {
      const column = event.target.closest(".pipeline-column");
      if (!column) return;
      if (!column.contains(event.relatedTarget)) column.classList.remove("is-drop-target");
    });

    boardRoot.addEventListener("drop", async (event) => {
      const column = event.target.closest(".pipeline-column");
      if (!column) return;
      event.preventDefault();
      column.classList.remove("is-drop-target");

      const targetStatus = column.getAttribute("data-status");
      const leadId = event.dataTransfer?.getData("text/plain") || state.dragLeadId;
      if (!targetStatus || !leadId) return;
      if (targetStatus === "Converted" && !config.canConvert) {
        showToast("Only organization admins can convert leads.", "danger");
        return;
      }
      await updateLeadStatus(leadId, targetStatus, false);
    });

    const modalEl = document.getElementById("pipelineLeadModal");
    if (modalEl) {
      modalEl.addEventListener("click", async (event) => {
        const stageButton = event.target.closest("[data-pipeline-stage-btn='1']");
        if (stageButton && state.selectedLeadId) {
          const targetStatus = stageButton.getAttribute("data-target-status");
          if (targetStatus) {
            await updateLeadStatus(state.selectedLeadId, targetStatus, targetStatus === "Converted");
          }
          return;
        }

        const completeButton = event.target.closest("[data-task-complete='1']");
        if (completeButton) {
          await completeTask(Number(completeButton.getAttribute("data-task-id")));
          return;
        }

        const quickButton = event.target.closest("[data-quick-task]");
        if (quickButton) {
          await applyQuickTask(quickButton.getAttribute("data-quick-task"));
          return;
        }

        if (event.target.id === "pipelineAddTaskBtn") {
          setTaskFormVisible(true);
          return;
        }

        if (event.target.id === "pipelineTaskCancelBtn") {
          setTaskFormVisible(false);
          resetTaskForm();
          return;
        }

        if (event.target.id === "pipelineTaskSaveBtn") {
          await saveTaskFromForm();
          return;
        }

        if (event.target.id === "pipelineDeleteTasksBtn") {
          await deleteSelectedTasks();
          return;
        }
      });
    }

    document.addEventListener("change", (event) => {
      if (!event.target.matches("#pipelineTasksList [data-task-select='1']")) return;
      const deleteButton = document.getElementById("pipelineDeleteTasksBtn");
      if (!deleteButton) return;
      const selectedCount = document.querySelectorAll("#pipelineTasksList [data-task-select='1']:checked").length;
      deleteButton.classList.toggle("d-none", selectedCount === 0);
      deleteButton.textContent = selectedCount > 0 ? `Delete Selected (${selectedCount})` : "Delete Selected";
    });
  }

  renderBoard();
  updateStats();
  bindEvents();
  let preferredView = "pipeline";
  try {
    preferredView = localStorage.getItem(viewStorageKey) === "table" ? "table" : "pipeline";
  } catch {
    preferredView = "pipeline";
  }
  setView(preferredView);
})();
