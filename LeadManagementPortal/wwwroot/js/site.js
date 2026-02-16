// Site-wide JavaScript

// Auto-hide alerts after 5 seconds
document.addEventListener("DOMContentLoaded", function () {
  const alerts = document.querySelectorAll(".alert-dismissible");
  alerts.forEach(function (alert) {
    setTimeout(function () {
      const bsAlert = new bootstrap.Alert(alert);
      bsAlert.close();
    }, 5000);
  });

  // Dependent dropdown: Sales Orgs by Sales Group - REMOVED (Handled in specific views to avoid conflicts)
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
