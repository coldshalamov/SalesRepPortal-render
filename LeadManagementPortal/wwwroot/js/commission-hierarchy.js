(function () {
  const seedElement = document.getElementById("commissionHierarchyInitialData");
  if (!seedElement) {
    return;
  }

  let state = JSON.parse(seedElement.textContent || "{}");
  let selectedAccountId = null;
  let graphIdToNodeId = new Map();

  const graphElement = document.getElementById("commissionGraph");
  const graphEmptyElement = document.getElementById("commissionGraphEmpty");
  const noticeElement = document.getElementById("commissionHierarchyNotice");
  const orphanListElement = document.getElementById("commissionOrphanList");
  const accountListElement = document.getElementById("commissionAccountList");
  const accountSearchElement = document.getElementById("commissionAccountSearch");
  const selectionEmptyElement = document.getElementById("commissionSelectionEmpty");
  const selectionContentElement = document.getElementById("commissionSelectionContent");
  const formElement = document.getElementById("commissionHierarchyForm");
  const saveButtonElement = document.getElementById("commissionSaveButton");

  const selectedNameElement = document.getElementById("commissionSelectedName");
  const selectedMetaElement = document.getElementById("commissionSelectedMeta");
  const selectedRoleElement = document.getElementById("commissionSelectedRole");
  const relationshipSummaryElement = document.getElementById("commissionRelationshipSummary");

  const accountIdInput = document.getElementById("commissionAccountId");
  const displayNameInput = document.getElementById("commissionDisplayName");
  const sponsorSelect = document.getElementById("commissionSponsorId");
  const dealTypeSelect = document.getElementById("commissionDealType");
  const calculationBasisSelect = document.getElementById("commissionCalculationBasis");
  const rateInput = document.getElementById("commissionRate");
  const baseCostInput = document.getElementById("commissionBaseCost");

  mermaid.initialize({
    startOnLoad: false,
    securityLevel: "loose",
    theme: "base",
    flowchart: {
      curve: "basis",
      useMaxWidth: true,
      htmlLabels: true
    },
    themeVariables: {
      fontFamily: "'Plus Jakarta Sans', system-ui, sans-serif",
      primaryColor: "#f7fbff",
      primaryBorderColor: "#7ca6ff",
      primaryTextColor: "#16233b",
      lineColor: "#6f8fbf"
    }
  });

  window.handleCommissionNodeClick = function handleCommissionNodeClick(graphId) {
    const nodeId = graphIdToNodeId.get(graphId);
    if (nodeId) {
      selectAccount(nodeId);
    }
  };

  accountSearchElement.addEventListener("input", renderAccountLists);

  formElement.addEventListener("submit", async function onSubmit(event) {
    event.preventDefault();

    const formData = new URLSearchParams(new FormData(formElement));
    saveButtonElement.disabled = true;
    saveButtonElement.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Saving...';

    try {
      const response = await fetch("/Commissions/SaveHierarchy", {
        method: "POST",
        headers: {
          "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
        },
        body: formData.toString()
      });

      const payload = await response.json();
      if (!response.ok) {
        throw new Error(payload.message || "Unable to save this hierarchy change.");
      }

      state = payload.hierarchy;
      selectedAccountId = accountIdInput.value;
      renderAll();
      showNotice(payload.message || "Hierarchy updated.", "info");
    } catch (error) {
      showNotice(error.message || "Unable to save this hierarchy change.", "danger");
    } finally {
      saveButtonElement.disabled = false;
      saveButtonElement.innerHTML = '<i class="bi bi-diagram-3 me-1"></i> Save Relationship';
    }
  });

  function renderAll() {
    const nodes = Array.isArray(state.nodes) ? state.nodes : [];
    if (!selectedAccountId || !nodes.some((node) => node.id === selectedAccountId)) {
      const firstOrphan = nodes.find((node) => node.isOrphan);
      selectedAccountId = firstOrphan ? firstOrphan.id : (nodes[0] ? nodes[0].id : null);
    }

    renderAccountLists();
    renderEditor();
    renderGraph();
  }

  function renderAccountLists() {
    const nodes = Array.isArray(state.nodes) ? state.nodes : [];
    const searchTerm = (accountSearchElement.value || "").trim().toLowerCase();
    const visibleNodes = searchTerm
      ? nodes.filter((node) => [node.fullName, node.email, node.role].some((value) => (value || "").toLowerCase().includes(searchTerm)))
      : nodes;

    orphanListElement.innerHTML = renderAccountButtons(visibleNodes.filter((node) => node.isOrphan));
    accountListElement.innerHTML = renderAccountButtons(visibleNodes);
    bindAccountButtons(orphanListElement);
    bindAccountButtons(accountListElement);
  }

  function renderAccountButtons(nodes) {
    if (!nodes.length) {
      return '<div class="commission-list-empty">No accounts match the current filter.</div>';
    }

    return nodes.map((node) => {
      const isSelected = node.id === selectedAccountId;
      const dealLabel = node.dealType ? humanize(node.dealType) : "No deal";
      const statusLabel = node.isOrphan ? "Root" : "Linked";
      return `
        <button type="button" class="commission-account-button${isSelected ? " is-selected" : ""}" data-account-id="${escapeAttribute(node.id)}">
          <span class="commission-account-button-head">
            <strong>${escapeHtml(node.fullName)}</strong>
            <span class="commission-account-button-state">${statusLabel}</span>
          </span>
          <span class="commission-account-button-meta">${escapeHtml(node.email || node.role)}</span>
          <span class="commission-account-button-tail">${escapeHtml(dealLabel)}</span>
        </button>
      `;
    }).join("");
  }

  function bindAccountButtons(container) {
    container.querySelectorAll("[data-account-id]").forEach((button) => {
      button.addEventListener("click", () => selectAccount(button.dataset.accountId));
    });
  }

  function selectAccount(accountId) {
    selectedAccountId = accountId;
    renderAll();
  }

  function renderEditor() {
    const node = getSelectedNode();
    if (!node) {
      selectionEmptyElement.classList.remove("d-none");
      selectionContentElement.classList.add("d-none");
      return;
    }

    selectionEmptyElement.classList.add("d-none");
    selectionContentElement.classList.remove("d-none");

    selectedNameElement.textContent = node.fullName;
    selectedMetaElement.textContent = node.email || "No email";
    selectedRoleElement.textContent = node.role || "Unassigned";
    relationshipSummaryElement.innerHTML = buildRelationshipSummary(node);

    accountIdInput.value = node.id;
    displayNameInput.value = node.fullName;
    sponsorSelect.innerHTML = buildSponsorOptions(node.id);
    sponsorSelect.value = node.sponsorId || "";
    dealTypeSelect.value = node.dealType || "";
    calculationBasisSelect.value = node.calculationBasis || "";
    rateInput.value = node.rate == null ? "" : node.rate;
    baseCostInput.value = node.baseCost == null ? "" : node.baseCost;
  }

  function buildRelationshipSummary(node) {
    const ownerName = node.sponsorName || "No owner assigned yet";
    const dealLabel = node.dealType ? humanize(node.dealType) : "No commission model configured";
    const basisLabel = node.calculationBasis ? humanize(node.calculationBasis) : "No basis selected";
    const rateLabel = node.rate == null
      ? "No rate set"
      : node.dealType === "Markup"
        ? `$${formatNumber(node.rate)} markup`
        : `${formatNumber(node.rate)}%`;

    return `
      <div class="commission-summary-row">
        <span class="commission-summary-label">Owner</span>
        <strong>${escapeHtml(ownerName)}</strong>
      </div>
      <div class="commission-summary-row">
        <span class="commission-summary-label">Model</span>
        <strong>${escapeHtml(dealLabel)}</strong>
      </div>
      <div class="commission-summary-row">
        <span class="commission-summary-label">Basis</span>
        <strong>${escapeHtml(basisLabel)}</strong>
      </div>
      <div class="commission-summary-row">
        <span class="commission-summary-label">Rate</span>
        <strong>${escapeHtml(rateLabel)}</strong>
      </div>
    `;
  }

  function buildSponsorOptions(currentAccountId) {
    const options = ['<option value="">-- No Owner / Root Account --</option>'];
    const selectedNode = getSelectedNode();
    const nodes = [...(state.nodes || [])]
      .filter((node) => node.id !== currentAccountId)
      .sort((left, right) => left.fullName.localeCompare(right.fullName));

    if (selectedNode && selectedNode.sponsorId && !nodes.some((node) => node.id === selectedNode.sponsorId)) {
      const hiddenSponsorLabel = selectedNode.sponsorName || "Owner outside current scope";
      options.push(
        `<option value="${escapeAttribute(selectedNode.sponsorId)}">${escapeHtml(hiddenSponsorLabel)} (outside your scope)</option>`
      );
    }

    nodes.forEach((node) => {
      options.push(`<option value="${escapeAttribute(node.id)}">${escapeHtml(node.fullName)} (${escapeHtml(node.role || "Unassigned")})</option>`);
    });

    return options.join("");
  }

  async function renderGraph() {
    const nodes = Array.isArray(state.nodes) ? state.nodes : [];
    graphElement.innerHTML = "";

    if (!nodes.length) {
      graphEmptyElement.classList.remove("d-none");
      return;
    }

    graphEmptyElement.classList.add("d-none");
    graphIdToNodeId = new Map();
    const graphDefinition = buildGraphDefinition(nodes);

    const renderId = `commissionHierarchyGraph${Date.now()}`;
    const result = await mermaid.render(renderId, graphDefinition);
    graphElement.innerHTML = result.svg;
    if (typeof result.bindFunctions === "function") {
      result.bindFunctions(graphElement);
    }
  }

  function buildGraphDefinition(nodes) {
    const lines = [
      "flowchart TD",
      "classDef rootNode fill:#fff4df,stroke:#d38b14,stroke-width:2px,color:#5d3900;",
      "classDef linkedNode fill:#f8fbff,stroke:#82a8f6,stroke-width:1.5px,color:#16233b;",
      "classDef selectedNode fill:#dff3ea,stroke:#198754,stroke-width:2.5px,color:#123524;"
    ];

    const idMap = new Map();
    nodes.forEach((node, index) => {
      const graphId = `node${index}`;
      idMap.set(node.id, graphId);
      graphIdToNodeId.set(graphId, node.id);

      const label = `${escapeMermaid(node.fullName)}<br/>${escapeMermaid(node.role || "Unassigned")}`;
      lines.push(`${graphId}["${label}"]`);
      lines.push(`click ${graphId} handleCommissionNodeClick "Edit ${escapeMermaid(node.fullName)}"`);
    });

    nodes.forEach((node) => {
      const graphId = idMap.get(node.id);
      if (!graphId || !node.sponsorId || !idMap.has(node.sponsorId)) {
        return;
      }

      const sponsorGraphId = idMap.get(node.sponsorId);
      const edgeLabel = buildEdgeLabel(node);
      lines.push(`${sponsorGraphId} -->${edgeLabel ? `|${edgeLabel}|` : ""} ${graphId}`);
    });

    const selectedGraphId = selectedAccountId ? idMap.get(selectedAccountId) : null;
    const rootIds = nodes.filter((node) => node.isOrphan).map((node) => idMap.get(node.id)).filter(Boolean);
    const linkedIds = nodes.filter((node) => !node.isOrphan).map((node) => idMap.get(node.id)).filter(Boolean);

    if (rootIds.length) {
      lines.push(`class ${rootIds.join(",")} rootNode;`);
    }

    if (linkedIds.length) {
      lines.push(`class ${linkedIds.join(",")} linkedNode;`);
    }

    if (selectedGraphId) {
      lines.push(`class ${selectedGraphId} selectedNode;`);
    }

    return lines.join("\n");
  }

  function buildEdgeLabel(node) {
    if (!node.dealType || node.rate == null) {
      return "linked";
    }

    if (node.dealType === "Markup") {
      return `$${formatNumber(node.rate)} markup`;
    }

    return `${formatNumber(node.rate)}% ${humanize(node.dealType)}`;
  }

  function getSelectedNode() {
    return (state.nodes || []).find((node) => node.id === selectedAccountId) || null;
  }

  function showNotice(message, tone) {
    noticeElement.textContent = message;
    noticeElement.classList.remove("d-none", "alert-info", "alert-danger", "alert-success");
    noticeElement.classList.add(tone === "danger" ? "alert-danger" : "alert-success");
  }

  function humanize(value) {
    return (value || "")
      .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
      .replace(/_/g, " ");
  }

  function formatNumber(value) {
    return Number(value).toLocaleString(undefined, {
      minimumFractionDigits: value % 1 === 0 ? 0 : 2,
      maximumFractionDigits: 2
    });
  }

  function escapeHtml(value) {
    return (value || "")
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#39;");
  }

  function escapeAttribute(value) {
    return escapeHtml(value);
  }

  function escapeMermaid(value) {
    return escapeHtml(value).replace(/\|/g, "&#124;");
  }

  renderAll();
})();
