/* org-chart.js — Organization page interactive chart */
(function () {
    'use strict';

    // ── Helpers ────────────────────────────────────────────────────────────

    function getToken() {
        const el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    async function apiPost(url, body) {
        const resp = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getToken()
            },
            body: JSON.stringify(body)
        });
        if (!resp.ok) throw new Error('Network error (' + resp.status + ')');
        return resp.json();
    }

    function setSpinner(spinnerId, btnId, loading) {
        const spinner = document.getElementById(spinnerId);
        const btn = document.getElementById(btnId);
        if (spinner) spinner.classList.toggle('d-none', !loading);
        if (btn) btn.disabled = loading;
    }

    function showError(elId, msg) {
        const el = document.getElementById(elId);
        if (!el) return;
        el.textContent = msg;
        el.classList.remove('d-none');
    }

    function hideError(elId) {
        const el = document.getElementById(elId);
        if (el) el.classList.add('d-none');
    }

    function getModal(id) {
        const el = document.getElementById(id);
        return el ? bootstrap.Modal.getOrCreateInstance(el) : null;
    }

    // ── Group Modal ────────────────────────────────────────────────────────

    let groupMode = 'create'; // 'create' | 'edit'

    function openGroupCreate() {
        groupMode = 'create';
        document.getElementById('groupModalTitle').textContent = 'Add Group';
        document.getElementById('groupModalId').value = '';
        document.getElementById('groupModalName').value = '';
        document.getElementById('groupModalDescription').value = '';
        hideError('groupModalError');
        getModal('groupModal').show();
        setTimeout(() => document.getElementById('groupModalName').focus(), 300);
    }

    function openGroupEdit(id, name, description) {
        groupMode = 'edit';
        document.getElementById('groupModalTitle').textContent = 'Edit Group';
        document.getElementById('groupModalId').value = id;
        document.getElementById('groupModalName').value = name;
        document.getElementById('groupModalDescription').value = description;
        hideError('groupModalError');
        getModal('groupModal').show();
        setTimeout(() => document.getElementById('groupModalName').focus(), 300);
    }

    document.getElementById('groupModalSave')?.addEventListener('click', async () => {
        const name = document.getElementById('groupModalName').value.trim();
        if (!name) { showError('groupModalError', 'Group name is required.'); return; }

        const id = document.getElementById('groupModalId').value;
        const description = document.getElementById('groupModalDescription').value.trim();

        hideError('groupModalError');
        setSpinner('groupModalSpinner', 'groupModalSave', true);

        try {
            const url = groupMode === 'create'
                ? '/Organization/CreateGroup'
                : '/Organization/EditGroup';

            const result = await apiPost(url, { id: id || null, name, description: description || null });

            if (result.ok) {
                getModal('groupModal').hide();
                location.reload();
            } else {
                showError('groupModalError', result.error || 'An error occurred.');
            }
        } catch (e) {
            showError('groupModalError', 'Request failed. Please try again.');
        } finally {
            setSpinner('groupModalSpinner', 'groupModalSave', false);
        }
    });

    // ── Org Modal ──────────────────────────────────────────────────────────

    let orgMode = 'create'; // 'create' | 'edit'

    function openOrgCreate(groupId) {
        orgMode = 'create';
        document.getElementById('orgModalTitle').textContent = 'Add Org';
        document.getElementById('orgModalId').value = '0';
        document.getElementById('orgModalGroupId').value = groupId;
        document.getElementById('orgModalName').value = '';
        hideError('orgModalError');
        getModal('orgModal').show();
        setTimeout(() => document.getElementById('orgModalName').focus(), 300);
    }

    function openOrgEdit(id, name, groupId) {
        orgMode = 'edit';
        document.getElementById('orgModalTitle').textContent = 'Edit Org';
        document.getElementById('orgModalId').value = id;
        document.getElementById('orgModalGroupId').value = groupId;
        document.getElementById('orgModalName').value = name;
        hideError('orgModalError');
        getModal('orgModal').show();
        setTimeout(() => document.getElementById('orgModalName').focus(), 300);
    }

    document.getElementById('orgModalSave')?.addEventListener('click', async () => {
        const name = document.getElementById('orgModalName').value.trim();
        if (!name) { showError('orgModalError', 'Org name is required.'); return; }

        const id = parseInt(document.getElementById('orgModalId').value, 10) || 0;
        const groupId = document.getElementById('orgModalGroupId').value;

        hideError('orgModalError');
        setSpinner('orgModalSpinner', 'orgModalSave', true);

        try {
            const url = orgMode === 'create'
                ? '/Organization/CreateOrg'
                : '/Organization/EditOrg';

            const result = await apiPost(url, { id, name, groupId });

            if (result.ok) {
                getModal('orgModal').hide();
                location.reload();
            } else {
                showError('orgModalError', result.error || 'An error occurred.');
            }
        } catch (e) {
            showError('orgModalError', 'Request failed. Please try again.');
        } finally {
            setSpinner('orgModalSpinner', 'orgModalSave', false);
        }
    });

    // ── Delete Modal ───────────────────────────────────────────────────────

    let deleteAction = null; // function to call on confirm

    function openDeleteModal(message, action) {
        document.getElementById('deleteModalMessage').textContent = message;
        hideError('deleteModalError');
        deleteAction = action;
        getModal('deleteModal').show();
    }

    document.getElementById('deleteModalConfirm')?.addEventListener('click', async () => {
        if (!deleteAction) return;

        setSpinner('deleteModalSpinner', 'deleteModalConfirm', true);
        hideError('deleteModalError');

        try {
            const result = await deleteAction();
            if (result.ok) {
                getModal('deleteModal').hide();
                location.reload();
            } else {
                showError('deleteModalError', result.error || 'Delete failed.');
            }
        } catch (e) {
            showError('deleteModalError', 'Request failed. Please try again.');
        } finally {
            setSpinner('deleteModalSpinner', 'deleteModalConfirm', false);
        }
    });

    // ── Event delegation ───────────────────────────────────────────────────

    document.addEventListener('click', (e) => {
        const btn = e.target.closest('[data-action]');
        if (!btn) return;

        const action = btn.dataset.action;

        if (action === 'edit-group') {
            openGroupEdit(btn.dataset.id, btn.dataset.name, btn.dataset.description || '');

        } else if (action === 'delete-group') {
            const id = btn.dataset.id;
            const name = btn.dataset.name;
            openDeleteModal(
                `Delete group "${name}"? This cannot be undone.`,
                () => apiPost('/Organization/DeleteGroup', { id })
            );

        } else if (action === 'add-org') {
            openOrgCreate(btn.dataset.groupId);

        } else if (action === 'edit-org') {
            openOrgEdit(btn.dataset.id, btn.dataset.name, btn.dataset.groupId);

        } else if (action === 'delete-org') {
            const id = parseInt(btn.dataset.id, 10);
            const name = btn.dataset.name;
            openDeleteModal(
                `Delete org "${name}"? This cannot be undone.`,
                () => apiPost('/Organization/DeleteOrg', { id })
            );
        }
    });

    // ── Top-level Add Group buttons ────────────────────────────────────────

    document.getElementById('btnAddGroup')?.addEventListener('click', openGroupCreate);
    document.getElementById('btnAddGroupEmpty')?.addEventListener('click', openGroupCreate);

    // ── Enter key in modals ────────────────────────────────────────────────

    ['groupModalName', 'groupModalDescription'].forEach(id => {
        document.getElementById(id)?.addEventListener('keydown', e => {
            if (e.key === 'Enter' && !e.shiftKey) document.getElementById('groupModalSave')?.click();
        });
    });

    document.getElementById('orgModalName')?.addEventListener('keydown', e => {
        if (e.key === 'Enter') document.getElementById('orgModalSave')?.click();
    });

})();
