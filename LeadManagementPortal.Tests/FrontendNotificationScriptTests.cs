using System;
using System.IO;
using Xunit;

namespace LeadManagementPortal.Tests
{
    public class FrontendNotificationScriptTests
    {
        private static string RepoFilePath(params string[] pathSegments) => Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                Path.Combine(pathSegments)));

        private static string NotificationsScriptPath => Path.GetFullPath(
            RepoFilePath(
                "LeadManagementPortal",
                "wwwroot",
                "js",
                "notifications.js"));

        private static string DashboardViewPath => Path.GetFullPath(
            RepoFilePath(
                "LeadManagementPortal",
                "Views",
                "Dashboard",
                "Index.cshtml"));

        private static string LoadScript() => File.ReadAllText(NotificationsScriptPath);
        private static string LoadDashboardView() => File.ReadAllText(DashboardViewPath);

        private static string ExtractBetween(string source, string startMarker, string endMarker)
        {
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Could not find marker: {startMarker}");

            var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
            Assert.True(end > start, $"Could not find end marker: {endMarker}");

            return source.Substring(start, end - start);
        }

        [Fact]
        public void UpdateDropdown_DoesNotRenderInlineJavascriptHandlers()
        {
            var script = LoadScript();

            Assert.DoesNotContain("onclick=\"DiRxNotifications.handleClick", script, StringComparison.Ordinal);
            Assert.DoesNotContain("onclick=\"DiRxNotifications.toggleRead", script, StringComparison.Ordinal);
            Assert.DoesNotContain("onclick=\"event.stopPropagation();", script, StringComparison.Ordinal);
        }

        [Fact]
        public void UpdateDropdown_StoresLinkAsEscapedDataAttribute()
        {
            var script = LoadScript();

            Assert.Contains("data-link=\"${this.escapeHtmlAttribute(n.link || '')}\"", script, StringComparison.Ordinal);
            Assert.Contains("handleDropdownClick", script, StringComparison.Ordinal);
        }

        [Fact]
        public void HandleClick_DoesNotBlockNavigationOnMarkReadFailure()
        {
            var script = LoadScript();
            var handleClick = ExtractBetween(script, "async handleClick(id, url, event)", "async toggleRead(id, event)");

            Assert.DoesNotContain("await this.postNotificationAction('mark_read'", handleClick, StringComparison.Ordinal);
            Assert.Contains("const markReadPromise = this.postNotificationAction('mark_read'", handleClick, StringComparison.Ordinal);
            Assert.Contains("window.location.href = target;", handleClick, StringComparison.Ordinal);
        }

        [Fact]
        public void ToggleRead_UsesCurrentDomState_NotStaleRenderedStatus()
        {
            var script = LoadScript();
            var toggleRead = ExtractBetween(script, "async toggleRead(id, event)", "setNotificationReadState(id, isRead)");

            Assert.DoesNotContain("currentStatus", toggleRead, StringComparison.Ordinal);
            Assert.Contains("item?.classList.contains('unread')", toggleRead, StringComparison.Ordinal);
        }

        [Fact]
        public void EscapeHtml_CoversSingleAndDoubleQuotes()
        {
            var script = LoadScript();

            Assert.Contains("case '\"':", script, StringComparison.Ordinal);
            Assert.Contains("return '&quot;';", script, StringComparison.Ordinal);
            Assert.Contains("case \"'\":", script, StringComparison.Ordinal);
            Assert.Contains("return '&#39;';", script, StringComparison.Ordinal);
        }

        [Fact]
        public void Start_IsIdempotent_ToPreventDuplicatePolling()
        {
            var script = LoadScript();

            Assert.Contains("if (this.pollerId !== null)", script, StringComparison.Ordinal);
            Assert.Contains("if (!window.DiRxNotifications)", script, StringComparison.Ordinal);
            Assert.Contains("DOMContentLoaded", script, StringComparison.Ordinal);
            Assert.Contains("{ once: true }", script, StringComparison.Ordinal);
        }

        [Fact]
        public void DashboardTrendScript_UsesJsonSerialization_NotManualJavascriptStringConstruction()
        {
            var view = LoadDashboardView();

            Assert.Contains("JsonSerializer.Serialize", view, StringComparison.Ordinal);
            Assert.DoesNotContain("Select(p => $\"\\\"{p.Label}\\\"\")", view, StringComparison.Ordinal);
        }

    }
}
