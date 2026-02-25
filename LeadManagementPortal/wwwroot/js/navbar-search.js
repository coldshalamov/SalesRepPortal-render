(() => {
  const MIN_QUERY_LEN = 2;
  const DEBOUNCE_MS = 300;

  function debounce(fn, waitMs) {
    let timeoutId = null;
    return (...args) => {
      if (timeoutId) clearTimeout(timeoutId);
      timeoutId = setTimeout(() => fn(...args), waitMs);
    };
  }

  function escapeHtml(text) {
    const div = document.createElement("div");
    div.textContent = text ?? "";
    return div.innerHTML;
  }

  function buildItemHtml(item) {
    const name = escapeHtml(item.name ?? "");
    const company = escapeHtml(item.company ?? "");
    const status = escapeHtml(item.status ?? "");

    const metaParts = [];
    if (company) metaParts.push(company);
    if (status) metaParts.push(status);
    const meta = metaParts.join(" • ");

    return `
      <a class="dropdown-item d-flex flex-column" href="${escapeHtml(item.url ?? "#")}">
        <span class="fw-semibold">${name}</span>
        ${meta ? `<span class="small text-muted">${meta}</span>` : ""}
      </a>
    `;
  }

  function buildSectionHtml(title, items) {
    if (!items || items.length === 0) return "";
    const header = `<div class="dropdown-header text-uppercase small">${escapeHtml(
      title
    )}</div>`;
    const rows = items.map(buildItemHtml).join("");
    return header + rows;
  }

  document.addEventListener("DOMContentLoaded", () => {
    const input = document.getElementById("navbarSearchInput");
    const menu = document.getElementById("navbarSearchMenu");
    const statusEl = document.getElementById("navbarSearchStatus");

    if (!input || !menu) return;

    let activeRequest = null;

    function setOpen(isOpen) {
      if (!menu) return;
      menu.classList.toggle("show", isOpen);
      menu.setAttribute("aria-hidden", String(!isOpen));
    }

    function setStatus(text) {
      if (!statusEl) return;
      statusEl.textContent = text ?? "";
    }

    function clearResults() {
      if (!menu) return;
      menu.innerHTML = "";
    }

    function renderEmpty(query) {
      clearResults();
      menu.innerHTML = `<div class="dropdown-item-text text-muted">No results for "${escapeHtml(
        query
      )}".</div>`;
      setOpen(true);
    }

    const fetchResults = debounce(async () => {
      const q = (input.value ?? "").trim();

      if (q.length < MIN_QUERY_LEN) {
        setStatus("");
        clearResults();
        setOpen(false);
        return;
      }

      setStatus("Searching…");

      if (activeRequest) {
        activeRequest.abort();
      }
      activeRequest = new AbortController();

      try {
        const res = await fetch(`/api/search?q=${encodeURIComponent(q)}`, {
          method: "GET",
          headers: { Accept: "application/json" },
          credentials: "same-origin",
          signal: activeRequest.signal,
        });

        if (!res.ok) {
          throw new Error(`Search failed: ${res.status}`);
        }

        const data = await res.json();
        const leads = Array.isArray(data?.leads) ? data.leads : [];
        const customers = Array.isArray(data?.customers) ? data.customers : [];

        clearResults();
        const html =
          buildSectionHtml("Leads", leads) +
          buildSectionHtml("Customers", customers);

        if (!html) {
          renderEmpty(q);
          setStatus("");
          return;
        }

        menu.innerHTML = html;
        setOpen(true);
        setStatus("");
      } catch (err) {
        if (err?.name === "AbortError") return;
        clearResults();
        menu.innerHTML =
          '<div class="dropdown-item-text text-danger">Search error. Try again.</div>';
        setOpen(true);
        setStatus("");
      }
    }, DEBOUNCE_MS);

    input.addEventListener("input", fetchResults);

    input.addEventListener("keydown", (e) => {
      if (e.key === "Escape") {
        setOpen(false);
        return;
      }

      if (e.key === "ArrowDown") {
        const firstLink = menu.querySelector("a.dropdown-item");
        if (firstLink) {
          e.preventDefault();
          firstLink.focus();
        }
      }
    });

    menu.addEventListener("keydown", (e) => {
      if (e.key === "Escape") {
        setOpen(false);
        input.focus();
      }
    });

    document.addEventListener("click", (e) => {
      const target = e.target;
      if (!target) return;

      const within =
        target === input || menu.contains(target) || target.closest?.("#navbarSearch");
      if (!within) setOpen(false);
    });
  });
})();

