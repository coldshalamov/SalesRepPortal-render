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
        this.listenersBound = false;
        this.pendingDropdownNotifications = null;
        this.forceDropdownUpdateOnNextPoll = false;
    }

    /**
     * Start polling for notifications.
     */
    start() {
        if (this.pollerId !== null) {
            return;
        }

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
    async postNotificationAction(action, notificationId = null, options = null) {
        const resolvedOptions = options || {};
        const body = resolvedOptions.body || {};
        if ((notificationId !== null && notificationId !== undefined) && body.notification_id === undefined) {
            body.notification_id = notificationId;
        }

        const headers = { 'Content-Type': 'application/json' };
        const antiforgeryToken = document.querySelector('meta[name="RequestVerificationToken"]')?.getAttribute('content');
        if (antiforgeryToken) {
            headers.RequestVerificationToken = antiforgeryToken;
        }

        const response = await fetch(`/api/notifications/${action}`, {
            method: 'POST',
            headers,
            credentials: 'same-origin',
            body: JSON.stringify(body),
            keepalive: resolvedOptions.keepalive === true
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
    async poll(options = null) {
        const requestedForceDropdownUpdate = options?.forceDropdownUpdate === true;
        if (this.pollInFlight) {
            this.pollPending = true;
            if (requestedForceDropdownUpdate) {
                this.forceDropdownUpdateOnNextPoll = true;
            }
            return;
        }
        this.pollInFlight = true;

        const forceDropdownUpdate = requestedForceDropdownUpdate || this.forceDropdownUpdateOnNextPoll;
        this.forceDropdownUpdateOnNextPoll = false;

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

            const canUpdateWhileOpen = forceDropdownUpdate === true;
            if (!this.isOpen || canUpdateWhileOpen) {
                this.updateDropdown(notifications);
                this.pendingDropdownNotifications = null;
            } else {
                this.pendingDropdownNotifications = notifications;
            }
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

        let html = `
            <div class="notif-bulk-bar" id="notifBulkBar" style="display:none;">
                <label class="notif-bulk-select-all">
                    <input type="checkbox" id="notifSelectAll">
                    <span id="notifSelectedCount">0</span> selected
                </label>
                <button class="notif-bulk-btn" id="notifBulkButton" type="button">Mark as read</button>
            </div>
        `;

        html += notifications.map(n => {
            const id = this.normalizeId(n.id);
            const isUnread = n.is_read === 0 || n.is_read === '0';

            return `
                <div class="notif-item${isUnread ? ' unread' : ''}"
                    data-notification-id="${id}"
                    data-is-read="${isUnread ? '0' : '1'}">
                    <div class="notif-checkbox">
                        <input type="checkbox"
                            class="notif-select-cb"
                            data-notif-id="${id}"
                            ${this.selectedNotifications.has(id) ? 'checked' : ''}>
                    </div>
                    <div class="notif-content"
                        data-link="${this.escapeHtmlAttribute(n.link || '')}">
                        <div class="notif-title">${this.escapeHtml(n.title)}</div>
                        <div class="notif-message">${this.escapeHtml(n.message)}</div>
                        <div class="notif-time">${this.formatTimeAgo(n.created_at)}</div>
                    </div>
                    <div class="notif-actions">
                        <button type="button"
                            class="notif-read-toggle"
                            title="${isUnread ? 'Mark as read' : 'Mark as unread'}">
                            <span class="notif-dot ${isUnread ? 'unread' : 'read'}"></span>
                        </button>
                    </div>
                </div>
            `;
        }).join('');

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

    markCheckboxSelection(id, checked) {
        const notificationId = this.normalizeId(id);
        if (notificationId <= 0) return;

        if (checked) {
            this.selectedNotifications.add(notificationId);
        } else {
            this.selectedNotifications.delete(notificationId);
        }

        this.updateSelectAllCheckbox();
        this.updateBulkBar();
    }

    toggleSelectAll() {
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
            await this.postNotificationAction('mark_read_bulk', null, { body: { notification_ids: ids } });
            await this.poll({ forceDropdownUpdate: true });
        } catch (err) {
            console.error('Failed to mark selected as read:', err);
        }
    }

    async markSelectedAsUnread() {
        const ids = Array.from(this.selectedNotifications).filter(id => id > 0);
        if (!ids.length) return;

        try {
            await this.postNotificationAction('mark_unread_bulk', null, { body: { notification_ids: ids } });
            await this.poll({ forceDropdownUpdate: true });
        } catch (err) {
            console.error('Failed to mark selected as unread:', err);
        }
    }

    async handleClick(id, url, event) {
        if (event && (event.target.closest('.notif-actions') || event.target.closest('.notif-checkbox'))) {
            return;
        }

        const notificationId = this.normalizeId(id);
        if (notificationId <= 0) return;

        this.toggleDropdown(false);
        this.setNotificationReadState(notificationId, true);

        const markReadPromise = this.postNotificationAction('mark_read', notificationId, { keepalive: true })
            .catch(err => {
                console.error('Failed to mark as read on click:', err);
            });

        const target = this.sanitizeNavigationTarget(url);
        if (target) {
            window.location.href = target;
            return;
        }

        await markReadPromise;
        void this.poll();
    }

    async toggleRead(id, event) {
        if (event) event.stopPropagation();

        const notificationId = this.normalizeId(id);
        if (notificationId <= 0) return;

        const item = document.querySelector(`[data-notification-id="${notificationId}"]`);
        const isUnread = item?.classList.contains('unread') || item?.dataset.isRead === '0';
        const action = isUnread ? 'mark_read' : 'mark_unread';

        try {
            await this.postNotificationAction(action, notificationId);
            this.setNotificationReadState(notificationId, action === 'mark_read');
            void this.poll();
        } catch (err) {
            console.error(`Failed to ${action} notification:`, err);
        }
    }

    setNotificationReadState(id, isRead) {
        const notificationId = this.normalizeId(id);
        if (notificationId <= 0) return;

        const item = document.querySelector(`[data-notification-id="${notificationId}"]`);
        if (!item) return;

        item.dataset.isRead = isRead ? '1' : '0';
        item.classList.toggle('unread', !isRead);

        const dot = item.querySelector('.notif-dot');
        if (dot) {
            dot.classList.toggle('unread', !isRead);
            dot.classList.toggle('read', isRead);
        }

        const btn = item.querySelector('.notif-read-toggle');
        if (btn) {
            btn.title = isRead ? 'Mark as unread' : 'Mark as read';
        }

        this.updateBulkBar();
    }

    sanitizeNavigationTarget(url) {
        if (typeof url !== 'string') return null;

        const trimmed = url.trim();
        if (trimmed.length === 0 || trimmed === 'null' || trimmed === 'undefined') {
            return null;
        }

        if (trimmed.startsWith('/')) {
            return trimmed;
        }

        if (/^https?:\/\//i.test(trimmed)) {
            return trimmed;
        }

        if (/^(javascript|data|vbscript):/i.test(trimmed)) {
            return null;
        }

        return `/${trimmed.replace(/^\/+/, '')}`;
    }

    async markAllRead() {
        try {
            await this.postNotificationAction('mark_all_read');
            await this.poll({ forceDropdownUpdate: true });
            return true;
        } catch (err) {
            console.error('Failed to mark all as read:', err);
            return false;
        }
    }

    toggleDropdown(force) {
        const dropdown = document.getElementById('notifDropdown');
        if (!dropdown) return;

        const wasOpen = this.isOpen;
        this.isOpen = force !== undefined ? force : !this.isOpen;

        if (this.isOpen) {
            dropdown.classList.add('active');
            if (!wasOpen) {
                void this.poll({ forceDropdownUpdate: true });
            }
        } else {
            dropdown.classList.remove('active');
            this.selectedNotifications.clear();
            if (this.pendingDropdownNotifications) {
                this.updateDropdown(this.pendingDropdownNotifications);
                this.pendingDropdownNotifications = null;
            }
        }
    }

    handleDropdownClick(event) {
        const target = event.target;

        const selectAll = target.closest('#notifSelectAll');
        if (selectAll) {
            this.toggleSelectAll();
            event.stopPropagation();
            return;
        }

        const selectCheckbox = target.closest('.notif-select-cb');
        if (selectCheckbox) {
            this.markCheckboxSelection(selectCheckbox.dataset.notifId, selectCheckbox.checked);
            event.stopPropagation();
            return;
        }

        const readToggle = target.closest('.notif-read-toggle');
        if (readToggle) {
            const item = readToggle.closest('.notif-item');
            void this.toggleRead(item?.dataset.notificationId, event);
            return;
        }

        const content = target.closest('.notif-content');
        if (content) {
            const item = content.closest('.notif-item');
            void this.handleClick(item?.dataset.notificationId, content.dataset.link || '', event);
        }
    }

    setupEventListeners() {
        if (this.listenersBound) {
            return;
        }
        this.listenersBound = true;

        const bellBtn = document.getElementById('notifBellBtn');
        if (bellBtn) {
            bellBtn.addEventListener('click', e => {
                e.stopPropagation();
                this.toggleDropdown();
            });
        }

        const dropdown = document.getElementById('notifDropdown');
        if (dropdown) {
            dropdown.addEventListener('click', e => this.handleDropdownClick(e));
        }

        const markAllButton = document.getElementById('notifMarkAllBtn');
        if (markAllButton) {
            markAllButton.addEventListener('click', e => {
                e.preventDefault();
                void this.markAllRead();
            });
        }

        document.addEventListener('click', e => {
            const currentDropdown = document.getElementById('notifDropdown');
            const bell = document.getElementById('notifBellBtn');

            if (currentDropdown && !currentDropdown.contains(e.target) && e.target !== bell && !bell?.contains(e.target)) {
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
        const input = String(text ?? '');
        return input.replace(/[&<>"']/g, char => {
            switch (char) {
                case '&':
                    return '&amp;';
                case '<':
                    return '&lt;';
                case '>':
                    return '&gt;';
                case '"':
                    return '&quot;';
                case "'":
                    return '&#39;';
                default:
                    return char;
            }
        });
    }

    escapeHtmlAttribute(text) {
        return this.escapeHtml(text).replace(/`/g, '&#96;');
    }
}

if (!window.DiRxNotifications) {
    window.DiRxNotifications = new NotificationPoller();
}

if (!window.SalesRepPortalNotifications) {
    window.SalesRepPortalNotifications = window.DiRxNotifications;
}

const startNotifications = () => window.DiRxNotifications.start();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', startNotifications, { once: true });
} else {
    startNotifications();
}
