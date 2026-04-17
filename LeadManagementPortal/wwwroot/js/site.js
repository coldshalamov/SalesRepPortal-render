// Site-wide JavaScript

function parseTickerNumber(rawValue) {
  if (rawValue === null || rawValue === undefined) {
    return 0;
  }

  const normalized = String(rawValue).replace(/[^0-9.\-]/g, "");
  const parsed = Number.parseFloat(normalized);
  return Number.isFinite(parsed) ? parsed : 0;
}

function createTickerFormatter(format, decimals, currencyCode) {
  if (format === "currency") {
    return new Intl.NumberFormat("en-US", {
      style: "currency",
      currency: currencyCode || "USD",
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals,
    });
  }

  return new Intl.NumberFormat("en-US", {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  });
}

function initializeNumberTickers() {
  const elements = document.querySelectorAll(".mui-number-ticker");
  if (!elements.length) {
    return;
  }

  const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const supportsObserver = "IntersectionObserver" in window;
  const observer = supportsObserver
    ? new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) {
            return;
          }

          const el = entry.target;
          animateNumberTicker(el, prefersReducedMotion);
          observer.unobserve(el);
        });
      },
      { threshold: 0.4 }
    )
    : null;

  elements.forEach((el) => {
    if (observer) {
      observer.observe(el);
    } else {
      animateNumberTicker(el, prefersReducedMotion);
    }
  });
}

function animateNumberTicker(el, prefersReducedMotion) {
  if (el.dataset.tickerAnimated === "true") {
    return;
  }
  el.dataset.tickerAnimated = "true";

  const value = parseTickerNumber(el.dataset.value);
  const startValue = el.dataset.start !== undefined
    ? parseTickerNumber(el.dataset.start)
    : 0;
  const decimals = Number.parseInt(el.dataset.decimals || "0", 10);
  const duration = Math.max(Number.parseInt(el.dataset.duration || "900", 10), 200);
  const prefix = el.dataset.prefix || "";
  const suffix = el.dataset.suffix || "";
  const format = (el.dataset.format || "number").toLowerCase();
  const formatter = createTickerFormatter(format, Number.isFinite(decimals) ? decimals : 0, el.dataset.currency);
  const render = (numericValue) => {
    el.textContent = `${prefix}${formatter.format(numericValue)}${suffix}`;
  };

  if (prefersReducedMotion) {
    render(value);
    return;
  }

  const startTime = performance.now();
  const delta = value - startValue;

  const step = (timestamp) => {
    const progress = Math.min((timestamp - startTime) / duration, 1);
    const eased = 1 - Math.pow(1 - progress, 3);
    render(startValue + delta * eased);

    if (progress < 1) {
      window.requestAnimationFrame(step);
    } else {
      render(value);
    }
  };

  window.requestAnimationFrame(step);
}

function initializeMagicCards() {
  const cards = document.querySelectorAll(".mui-magic-card");
  cards.forEach((card) => {
    const setCenter = () => {
      card.style.setProperty("--mx", "50%");
      card.style.setProperty("--my", "50%");
    };

    setCenter();

    card.addEventListener("pointermove", (event) => {
      if (event.pointerType === "touch") {
        return;
      }

      const rect = card.getBoundingClientRect();
      if (!rect.width || !rect.height) {
        return;
      }

      const x = ((event.clientX - rect.left) / rect.width) * 100;
      const y = ((event.clientY - rect.top) / rect.height) * 100;
      card.style.setProperty("--mx", `${x.toFixed(2)}%`);
      card.style.setProperty("--my", `${y.toFixed(2)}%`);
    });

    card.addEventListener("pointerleave", setCenter);
    card.addEventListener("blur", setCenter, true);
  });
}

// Auto-hide alerts after 5 seconds
document.addEventListener("DOMContentLoaded", function () {
  const alerts = document.querySelectorAll(".alert-dismissible");
  alerts.forEach(function (alert) {
    setTimeout(function () {
      const bsAlert = new bootstrap.Alert(alert);
      bsAlert.close();
    }, 5000);
  });

  // Enable Bootstrap tooltips/popovers globally when present
  document
    .querySelectorAll('[data-bs-toggle="tooltip"]')
    .forEach((el) => new bootstrap.Tooltip(el));

  document
    .querySelectorAll('[data-bs-toggle="popover"]')
    .forEach((el) => new bootstrap.Popover(el));

  // Dependent dropdown: Sales Orgs by Sales Group - REMOVED (Handled in specific views to avoid conflicts)
  initializeNumberTickers();
  initializeMagicCards();
});

// Confirm dialogs for delete actions
function confirmDelete(message) {
  return confirm(message || "Are you sure you want to delete this item?");
}

// Format phone numbers
function formatPhoneNumber(phoneNumberString) {
  const cleaned = ("" + phoneNumberString).replace(/\D/g, "");
  const match = cleaned.match(/^(\d{3})(\d{3})(\d{4})$/);
  if (match) {
    return "(" + match[1] + ") " + match[2] + "-" + match[3];
  }
  return phoneNumberString;
}
