(function () {
  function dispatchNativeSelectionEvents(select) {
    select.dispatchEvent(new Event("input", { bubbles: true }));
    select.dispatchEvent(new Event("change", { bubbles: true }));
  }

  function enhance(select) {
    if (select.dataset.enhanced) return;

    const wrapper = document.createElement("div");
    wrapper.className = "multi-select-wrapper";

    const display = document.createElement("button");
    display.type = "button";
    display.className = "multi-select-display";
    display.setAttribute("aria-haspopup", "listbox");
    display.setAttribute("aria-expanded", "false");

    const menu = document.createElement("div");
    menu.className = "multi-select-menu";
    menu.setAttribute("role", "listbox");
    menu.setAttribute("aria-multiselectable", "true");

    select.style.display = "none";
    select.parentNode.insertBefore(wrapper, select);
    wrapper.appendChild(display);
    wrapper.appendChild(menu);
    wrapper.appendChild(select);

    let entries = [];
    let activeIndex = -1;

    function setOpen(open, options = {}) {
      const shouldOpen = !!open;
      wrapper.classList.toggle("open", shouldOpen);
      display.setAttribute("aria-expanded", shouldOpen ? "true" : "false");

      if (!shouldOpen && options.focusDisplay !== false) {
        display.focus();
      }
    }

    function setActiveIndex(index, focus = false) {
      if (!entries.length) {
        activeIndex = -1;
        return;
      }

      const bounded = Math.max(0, Math.min(index, entries.length - 1));
      activeIndex = bounded;

      entries.forEach((entry, i) => {
        entry.item.tabIndex = i === bounded ? 0 : -1;
      });

      if (focus) {
        entries[bounded].item.focus();
      }
    }

    function syncItemState(entry) {
      const selected = !!entry.option.selected;
      entry.item.classList.toggle("selected", selected);
      entry.item.setAttribute("aria-selected", selected ? "true" : "false");
    }

    function syncFromSelect() {
      entries.forEach(syncItemState);

      if (entries.length && activeIndex < 0) {
        const firstSelected = entries.findIndex((entry) => entry.option.selected);
        setActiveIndex(firstSelected >= 0 ? firstSelected : 0, false);
      }
    }

    function updateDisplay() {
      const selected = Array.from(select.selectedOptions).map((option) => option.text);
      display.textContent = selected.length
        ? selected.join(", ")
        : "Select " + (select.getAttribute("name") || "items");
    }

    function applyToggle(entry) {
      entry.option.selected = !entry.option.selected;
      syncItemState(entry);
      updateDisplay();
      dispatchNativeSelectionEvents(select);
    }

    function moveActive(delta) {
      if (!entries.length) return;
      const current = activeIndex >= 0 ? activeIndex : 0;
      const next = Math.max(0, Math.min(current + delta, entries.length - 1));
      setActiveIndex(next, true);
    }

    function handleItemKeydown(event, entry, index) {
      switch (event.key) {
        case "ArrowDown":
          event.preventDefault();
          moveActive(1);
          break;
        case "ArrowUp":
          event.preventDefault();
          moveActive(-1);
          break;
        case "Home":
          event.preventDefault();
          setActiveIndex(0, true);
          break;
        case "End":
          event.preventDefault();
          setActiveIndex(entries.length - 1, true);
          break;
        case " ":
        case "Spacebar":
        case "Enter":
          event.preventDefault();
          applyToggle(entry);
          setActiveIndex(index, true);
          break;
        case "Escape":
          event.preventDefault();
          setOpen(false);
          break;
        case "Tab":
          setOpen(false, { focusDisplay: false });
          break;
      }
    }

    function buildMenu() {
      menu.innerHTML = "";
      entries = [];
      activeIndex = -1;

      const options = Array.from(select.options);
      if (!options.length) {
        const empty = document.createElement("div");
        empty.className = "multi-select-empty";
        empty.textContent = "No options";
        menu.appendChild(empty);
        return;
      }

      options.forEach((option, index) => {
        const item = document.createElement("div");
        item.className = "multi-select-item";
        item.setAttribute("role", "option");
        item.setAttribute("aria-selected", option.selected ? "true" : "false");
        item.dataset.value = option.value;
        item.tabIndex = -1;

        const check = document.createElement("span");
        check.className = "checkmark";
        check.innerHTML = "&#10003;";

        const label = document.createElement("span");
        label.textContent = option.text;

        item.appendChild(check);
        item.appendChild(label);

        const entry = { item, option };
        syncItemState(entry);

        item.addEventListener("click", function (event) {
          event.stopPropagation();
          applyToggle(entry);
          setActiveIndex(index, true);
        });

        item.addEventListener("keydown", function (event) {
          handleItemKeydown(event, entry, index);
        });

        entries.push(entry);
        menu.appendChild(item);
      });

      const firstSelected = entries.findIndex((entry) => entry.option.selected);
      setActiveIndex(firstSelected >= 0 ? firstSelected : 0, false);
    }

    display.addEventListener("click", function (event) {
      event.stopPropagation();
      const open = !wrapper.classList.contains("open");
      setOpen(open, { focusDisplay: false });
    });

    display.addEventListener("keydown", function (event) {
      if (event.key === "ArrowDown") {
        event.preventDefault();
        setOpen(true, { focusDisplay: false });
        setActiveIndex(activeIndex >= 0 ? activeIndex : 0, true);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        setOpen(true, { focusDisplay: false });
        setActiveIndex(activeIndex >= 0 ? activeIndex : entries.length - 1, true);
      } else if (event.key === " " || event.key === "Spacebar" || event.key === "Enter") {
        event.preventDefault();
        const open = !wrapper.classList.contains("open");
        setOpen(open, { focusDisplay: !open });
        if (open) {
          setActiveIndex(activeIndex >= 0 ? activeIndex : 0, true);
        }
      } else if (event.key === "Escape") {
        setOpen(false);
      }
    });

    document.addEventListener("click", function (event) {
      if (!wrapper.contains(event.target) && wrapper.classList.contains("open")) {
        setOpen(false, { focusDisplay: false });
      }
    });

    select.addEventListener("change", function () {
      syncFromSelect();
      updateDisplay();
    });

    buildMenu();
    updateDisplay();
    select.dataset.enhanced = "true";
  }

  function init(root = document) {
    root
      .querySelectorAll('select[multiple][data-enhance="multiselect"]')
      .forEach(enhance);
  }

  if (!window.DiRxMultiSelect) {
    window.DiRxMultiSelect = {};
  }

  window.DiRxMultiSelect.enhance = enhance;
  window.DiRxMultiSelect.init = function (root) {
    init(root || document);
  };

  if (!window.DiRxMultiSelect._bootstrapped) {
    window.DiRxMultiSelect._bootstrapped = true;

    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", function () {
        window.DiRxMultiSelect.init();
      }, { once: true });
    } else {
      window.DiRxMultiSelect.init();
    }
  }
})();
