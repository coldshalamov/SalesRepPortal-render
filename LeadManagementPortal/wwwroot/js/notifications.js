/**
 * Notification Poller
 *
 * Polls /api/notifications every 30 seconds to check for new in-app
 * notifications. Updates the bell badge and dropdown in _Layout.cshtml.
 *
 * Ported from TheRxSpot.com notification-poller.js and adapted for
 * ASP.NET Core cookie-based auth (no CSRF token injection required for GETs;
 * POST actions include the antiforgery token if present in the page meta tag).
 */

class NotificationPoller {
    constructor() {
        this.interval = 30000; // 30 seconds
        this.pollerId = null;
        this.isOpen = false;
        this.selectedNotifications = new Set();
        this.pollInFlight = false;
        this.pollPending = false;
        this.notificationLimit = 50;
    }

    /**
     * Start polling for notifications.
     */
    start() {
        void this.poll();
        this.pollerId = setInterval(() => void this.poll(), this.interval);
        this.setupEventListeners();
    }

    /**
     * Stop polling.
     */
    stop() {
        if (this.pollerId) {
            clearInterval(this.pollerId);
            this.pollerId = null;
        }
    }

    /**
     * Send a POST action to the notifications API.
     * Uses fetch with credentials (auth cookie is sent automatically).
     */
    async postNotificationAction(action, notificationId = null) {
        const body = {};
        if (notificationId !== null && notificationId !== undefined) {
            body.notification_id = notificationId;
        }

        const response = await fetch(`/api/notifications/${action}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify(body)
        });

        let data = null;
        try {
            data = await response.json();
        } catch {
            throw new Error(`Notification API returned non-JSON response (status ${response.status})`);
        }

        if (response.ok && data?.success) {
            return data;
        }

        const message = data?.error?.message || data?.message || `Notification API failed (${response.status})`;
        throw new Error(message);
    }

    /**
     * Poll the API for notifications.
     */
    async poll() {
        if (this.pollInFlight) {
            this.pollPending = true;
            return;
        }
        this.pollInFlight = true;

        try {
            const response = await fetch(
                `/api/notifications/get_notifications?limit=${this.notificationLimit}&include_unread_count=true`,
                { cache: 'no-store', credentials: 'same-origin' }
            );
            const data = await response.json();

            if (!response.ok || !data?.success) {
                const message = data?.error?.message || `Unable to load notifications (${response.status})`;
                this.renderLoadError(message);
                return;
            }

            const payload = data?.data ?? data;
            const notifications = payload.notifications || [];
            const parsedCount = Number.parseInt(payload.unread_count, 10);

            if (Number.isFinite(parsedCount)) {
                this.updateBadge(Math.max(0, parsedCount));
            } else {
                this.updateBadge(notifications);
            }
            this.updateDropdown(notifications);
        } catch (error) {
            console.error('Notification poll failed:', error);
            this.renderLoadError('Unable to load notifications right now.');
        } finally {
            this.pollInFlight = false;
            if (this.pollPending) {
                this.pollPending = false;
                void this.poll();
            }
        }
    }

    renderLoadError(message) {
        const list = document.getElementById('notifList');
        if (list) {
            list.innerHTML = `
                <div class="notif-empty">
                    <div>${this.escapeHtml(message || 'Unable to load notifications.')}</div>
                </div>
            `;
        }
    }

    /**
     * Update the badge count on the bell icon.
     */
    updateBadge(notificationsOrCount) {
        let unreadCount = 0;
        if (typeof notificationsOrCount === 'number') {
            unreadCount = Math.max(0, notificationsOrCount);
        } else {
            const list = Array.isArray(notificationsOrCount) ? notificationsOrCount : [];
            unreadCount = list.filter(n => n.is_read === 0 || n.is_read === '0').length;
        }

        const badge = document.getElementById('notifBadge');
        if (badge) {
            if (unreadCount > 0) {
                badge.textContent = unreadCount > 99 ? '99+' : unreadCount;
                badge.style.display = 'flex';
            } else {
                badge.style.display = 'none';
            }
        }
    }

    /**
     * Render the dropdown list of notifications.
     */
    updateDropdown(notifications) {
        const list = document.getElementById('notifList');
        if (!list) return;

        if (!notifications.length) {
            list.innerHTML = `
                <div class="notif-empty">
                    <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"/>
                        <path d="M13.73 21a2 2 0 0 1-3.46 0"/>
                    </svg>
                    <div>No new notifications</div>
                </div>
            `;
            return;
        }

        // Bulk action header
        let html = `
            <div class="notif-bulk-bar" id="notifBulkBar" style="display:none;">
                <label class="notif-bulk-select-all">
                    <input type="checkbox" id="notifSelectAll" onclick="DiRxNotifications.toggleSelectAll(event)">
                    <span id="notifSelectedCount">0</span> selected
                </label>
                <button class="notif-bulk-btn" id="notifBulkButton" type="button">Mark as read</button>
            </div>
        `;

        html += notifications.map(n => `
            <div class="notif-item${n.is_read === 0 || n.is_read === '0' ? ' unread' : ''}"
                 data-notification-id="${n.id}">
                <div class="notif-checkbox">
                    <input type="checkbox"
                           class="notif-select-cb"
                           data-notif-id="${n.id}"
                           ${this.selectedNotifications.has(this.normalizeId(n.id)) ? 'checked' : ''}
                           onclick="event.stopPropagation(); DiRxNotifications.toggleSelection(${n.id})">
                </div>
                <div class="notif-content"
                     onclick="DiRxNotifications.handleClick(${n.id}, '${this.escapeHtml(n.link || '')}', event)">
                    <div class="notif-title">${this.escapeHtml(n.title)}</div>
                    <div class="notif-message">${this.escapeHtml(n.message)}</div>
                    <div class="notif-time">${this.formatTimeAgo(n.created_at)}</div>
                </div>
                <div class="notif-actions">
                    <button type="button"
                            class="notif-read-toggle"
                            title="${n.is_read === 0 || n.is_read === '0' ? 'Mark as read' : 'Mark as unread'}"
                            onclick="DiRxNotifications.toggleRead(${n.id}, ${n.is_read}, event)">
                        <span class="notif-dot ${n.is_read === 0 || n.is_read === '0' ? 'unread' : 'read'}"></span>
                    </button>
                </div>
            </div>
        `).join('');

        html += `
            <div class="notif-footer">
                <a href="/Leads" class="notif-view-all">
                    View All Leads
                    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                        <polyline points="9 18 15 12 9 6"></polyline>
                    </svg>
                </a>
            </div>
        `;

        list.innerHTML = html;
        this.updateSelectAllCheckbox();
        this.updateBulkBar();
    }

    normalizeId(value) {
        const id = Number.parseInt(value, 10);
        return Number.isInteger(id) ? id : 0;
    }

    toggleSelection(id) {
        const nid = this.normalizeId(id);
        if (nid <= 0) return;
        if (this.selectedNotifications.has(nid)) {
            this.selectedNotifications.delete(nid);
        } else {
            this.selectedNotifications.add(nid);
        }
        this.updateSelectAllCheckbox();
        this.updateBulkBar();
    }

    toggleSelectAll(event) {
        if (event) event.stopPropagation();
        const checkboxes = document.querySelectorAll('.notif-select-cb');
        const selectAll = document.getElementById('notifSelectAll');
        const visibleIds = Array.from(checkboxes)
            .map(cb => this.normalizeId(cb.dataset.notifId))
            .filter(id => id > 0);

        if (selectAll && selectAll.checked) {
            visibleIds.forEach(id => this.selectedNotifications.add(id));
            checkboxes.forEach(cb => { cb.checked = true; });
        } else {
            visibleIds.forEach(id => this.selectedNotifications.delete(id));
            checkboxes.forEach(cb => { cb.checked = false; });
        }
        this.updateSelectAllCheckbox();
        this.updateBulkBar();
    }

    updateSelectAllCheckbox() {
        const selectAll = document.getElementById('notifSelectAll');
        if (!selectAll) return;

        const checkboxes = document.querySelectorAll('.notif-select-cb');
        const visibleIds = Array.from(checkboxes)
            .map(cb => this.normalizeId(cb.dataset.notifId))
            .filter(id => id > 0);
        const visibleSet = new Set(visibleIds);

        // Drop stale selections
        Array.from(this.selectedNotifications).forEach(id => {
            if (!visibleSet.has(id)) this.selectedNotifications.delete(id);
        });

        const total = visibleIds.length;
        const checked = this.selectedNotifications.size;

        if (total === 0) {
            selectAll.checked = false;
            selectAll.indeterminate = false;
        } else if (checked === 0) {
            selectAll.checked = false;
            selectAll.indeterminate = false;
        } else if (checked === total) {
            selectAll.checked = true;
            selectAll.indeterminate = false;
        } else {
            selectAll.checked = false;
            selectAll.indeterminate = true;
        }
    }

    updateBulkBar() {
        const bar = document.getElementById('notifBulkBar');
        const countEl = document.getElementById('notifSelectedCount');
        const btn = document.getElementById('notifBulkButton');

        if (!bar) return;

        if (this.selectedNotifications.size > 0) {
            bar.style.display = 'flex';
            if (countEl) countEl.textContent = this.selectedNotifications.size;

            const hasUnread = Array.from(this.selectedNotifications).some(id => {
                const item = document.querySelector(`[data-notification-id="${id}"]`);
                return item?.classList.contains('unread');
            });

            if (btn) {
                btn.textContent = hasUnread ? 'Mark as read' : 'Mark as unread';
                btn.onclick = hasUnread
                    ? () => this.markSelectedAsRead()
                    : () => this.markSelectedAsUnread();
            }
        } else {
            bar.style.display = 'none';
        }
    }

    async markSelectedAsRead() {
        const ids = Array.from(this.selectedNotifications).filter(id => id > 0);
        if (!ids.length) return;
        try {
            await Promise.all(ids.map(id => this.postNotificationAction('mark_read', id)));
            await this.poll();
        } catch (err) {
            console.error('Failed to mark selected as read:', err);
        }
    }

    async markSelectedAsUnread() {
        const ids = Array.from(this.selectedNotifications).filter(id => id > 0);
        if (!ids.length) return;
        try {
            await Promise.all(ids.map(id => this.postNotificationAction('mark_unread', id)));
            await this.poll();
        } catch (err) {
            console.error('Failed to mark selected as unread:', err);
        }
    }

    async handleClick(id, url, event) {
        if (event && (event.target.closest('.notif-actions') || event.target.closest('.notif-checkbox'))) return;

        try {
            await this.postNotificationAction('mark_read', id);
            this.toggleDropdown(false);

            if (url && url !== '' && url !== 'null' && url !== 'undefined') {
                let target = url;
                if (!url.startsWith('http') && !url.startsWith('/')) target = '/' + url;
                window.location.href = target;
            } else {
                void this.poll();
            }
        } catch (err) {
            console.error('Failed to mark as read on click:', err);
        }
    }

    async toggleRead(id, currentStatus, event) {
        if (event) event.stopPropagation();
        const action = (currentStatus === 0 || currentStatus === '0') ? 'mark_read' : 'mark_unread';
        try {
            await this.postNotificationAction(action, id);
            void this.poll();
        } catch (err) {
            console.error(`Failed to ${action} notification:`, err);
        }
    }

    async markAllRead() {
        try {
            await this.postNotificationAction('mark_all_read');
            await this.poll();
            return true;
        } catch (err) {
            console.error('Failed to mark all as read:', err);
            return false;
        }
    }

    /**
     * Optimistically mark all visible items as read in the DOM before the
     * server responds — gives instant feedback when the dropdown opens.
     */
    markCurrentListReadOptimistic() {
        document.querySelectorAll('#notifList .notif-item.unread').forEach(item => {
            item.classList.remove('unread');
            const dot = item.querySelector('.notif-dot');
            if (dot) { dot.classList.remove('unread'); dot.classList.add('read'); }
            const btn = item.querySelector('.notif-read-toggle');
            if (btn) btn.title = 'Mark as unread';
        });

        const badge = document.getElementById('notifBadge');
        if (badge) { badge.style.display = 'none'; badge.textContent = '0'; }
    }

    toggleDropdown(force) {
        const dropdown = document.getElementById('notifDropdown');
        if (!dropdown) return;

        const wasOpen = this.isOpen;
        this.isOpen = force !== undefined ? force : !this.isOpen;

        if (this.isOpen) {
            dropdown.classList.add('active');
            if (!wasOpen) {
                this.markCurrentListReadOptimistic();
                void this.markAllRead();
            }
        } else {
            dropdown.classList.remove('active');
            this.selectedNotifications.clear();
        }
    }

    setupEventListeners() {
        const bellBtn = document.getElementById('notifBellBtn');
        if (bellBtn) {
            bellBtn.addEventListener('click', e => { e.stopPropagation(); this.toggleDropdown(); });
        }

        document.addEventListener('click', e => {
            const dropdown = document.getElementById('notifDropdown');
            const bell = document.getElementById('notifBellBtn');
            if (dropdown && !dropdown.contains(e.target) && e.target !== bell && !bell?.contains(e.target)) {
                this.toggleDropdown(false);
            }
        });
    }

    formatTimeAgo(timestamp) {
        const now = new Date();
        const then = new Date(timestamp);
        const seconds = Math.floor((now - then) / 1000);

        if (seconds < 60) return 'Just now';
        const minutes = Math.floor(seconds / 60);
        if (minutes < 60) return `${minutes}m ago`;
        const hours = Math.floor(minutes / 60);
        if (hours < 24) return `${hours}h ago`;
        const days = Math.floor(hours / 24);
        if (days < 7) return `${days}d ago`;
        return then.toLocaleDateString();
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text || '';
        return div.innerHTML;
    }
}

// Create global instance
window.DiRxNotifications = new NotificationPoller();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => window.DiRxNotifications.start());
} else {
    window.DiRxNotifications.start();
}
