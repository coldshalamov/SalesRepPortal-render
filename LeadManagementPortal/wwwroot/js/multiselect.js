(function () {
  function enhance(select) {
    if (select.dataset.enhanced) return; // avoid double
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
    select.style.display = "none";
    select.parentNode.insertBefore(wrapper, select);
    wrapper.appendChild(display);
    wrapper.appendChild(menu);
    wrapper.appendChild(select);

    function buildMenu() {
      menu.innerHTML = "";
      const opts = Array.from(select.options);
      if (!opts.length) {
        const empty = document.createElement("div");
        empty.className = "multi-select-empty";
        empty.textContent = "No options";
        menu.appendChild(empty);
        return;
      }
      opts.forEach((o) => {
        const item = document.createElement("div");
        item.className = "multi-select-item" + (o.selected ? " selected" : "");
        item.setAttribute("role", "option");
        item.dataset.value = o.value;
        const check = document.createElement("span");
        check.className = "checkmark";
        check.innerHTML = "&#10003;";
        const label = document.createElement("span");
        label.textContent = o.text;
        item.appendChild(check);
        item.appendChild(label);
        item.addEventListener("click", function (e) {
          e.stopPropagation();
          o.selected = !o.selected;
          item.classList.toggle("selected", o.selected);
          updateDisplay();
        });
        menu.appendChild(item);
      });
    }

    function updateDisplay() {
      const selected = Array.from(select.selectedOptions).map((o) => o.text);
      display.textContent = selected.length
        ? selected.join(", ")
        : "Select " + (select.getAttribute("name") || "items");
      display.appendChild(document.createElement("span")); // placeholder to keep arrow via :after
    }

    display.addEventListener("click", function (e) {
      e.stopPropagation();
      const open = wrapper.classList.toggle("open");
      display.setAttribute("aria-expanded", open ? "true" : "false");
    });

    document.addEventListener("click", function (e) {
      if (!wrapper.contains(e.target)) {
        if (wrapper.classList.contains("open")) {
          wrapper.classList.remove("open");
          display.setAttribute("aria-expanded", "false");
        }
      }
    });

    select.addEventListener("change", updateDisplay);

    buildMenu();
    updateDisplay();
    select.dataset.enhanced = "true";
  }

  function init() {
    document
      .querySelectorAll('select[multiple][data-enhance="multiselect"]')
      .forEach(enhance);
  }
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
