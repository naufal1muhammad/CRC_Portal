// @ts-nocheck
(function () {
    let dt = null;
    let msgEl;

    function initSelect2() {
        if (window.jQuery && $.fn.select2) {
            $('.select2').select2({
                width: '100%',
                placeholder: '-- All --',
                allowClear: true,
                theme: 'bootstrap-5'
            });
        }
    }

    function setSelectOptions(selectEl, items, placeholderText) {
        if (!selectEl) return;

        while (selectEl.firstChild) {
            selectEl.removeChild(selectEl.firstChild);
        }

        const placeholder = document.createElement('option');
        placeholder.value = '';
        placeholder.textContent = placeholderText || '-- All --';
        selectEl.appendChild(placeholder);

        (items || []).forEach(i => {
            const value = (i?.id ?? i?.value ?? i?.name ?? '').toString();
            if (!value) return;

            const text = (i?.name ?? i?.text ?? i?.label ?? value).toString();
            const opt = document.createElement('option');
            opt.value = value;
            opt.textContent = text;
            selectEl.appendChild(opt);
        });
    }

    async function loadLookups() {
        msgEl = document.getElementById('auditTrailsMessage');
        if (msgEl) {
            msgEl.textContent = '';
            msgEl.classList.remove('text-success');
            msgEl.classList.add('text-danger');
        }

        try {
            const response = await fetch('/AuditTrails/GetLookups', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                if (msgEl) msgEl.textContent = 'Error loading filters.';
                return;
            }

            const result = await response.json();
            if (!result.success) {
                if (msgEl) msgEl.textContent = result.message || 'Error loading filters.';
                return;
            }

            setSelectOptions(document.getElementById('filterUserId'), result.users || [], '-- All Users --');
            setSelectOptions(document.getElementById('filterAction'), result.actions || [], '-- All Actions --');
            setSelectOptions(document.getElementById('filterCategory'), result.categories || [], '-- All Categories --');

            initSelect2();

            if (msgEl) {
                msgEl.textContent = '';
                msgEl.classList.remove('text-danger');
            }
        } catch (err) {
            console.error('Error loading audit trail lookups', err);
            if (msgEl) msgEl.textContent = 'Error loading filters.';
        }
    }

    function buildSearchPayload() {
        const userIdRaw = (document.getElementById('filterUserId')?.value || '').trim();
        const userId = userIdRaw ? parseInt(userIdRaw, 10) : null;

        const fromDate = document.getElementById('filterFromDate')?.value || '';
        const toDate = document.getElementById('filterToDate')?.value || '';

        const action = (document.getElementById('filterAction')?.value || '').trim();
        const category = (document.getElementById('filterCategory')?.value || '').trim();

        return {
            userId: Number.isFinite(userId) ? userId : null,
            fromDate: fromDate || null,
            toDate: toDate || null,
            action: action || null,
            category: category || null
        };
    }

    function escapeHtml(str) {
        return (str ?? '').toString().replace(/[&<>"']/g, s => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[s]));
    }

    function initOrUpdateDataTable(rows) {
        const $table = $('#auditTrailsTable');

        if (dt) {
            dt.clear();
            dt.rows.add(rows);
            dt.draw();
            return;
        }

        // Enable proper sorting on our datetime string
        if ($.fn?.dataTable?.moment) {
            $.fn.dataTable.moment('DD/MM/YYYY HH:mm');
        }

        dt = $table.DataTable({
            data: rows,
            columns: [
                { data: 'userId', title: 'User ID' },
                {
                    data: 'name',
                    title: 'Name',
                    render: function (data, type) {
                        if (type !== 'display') return data || '';
                        return escapeHtml(data || '');
                    }
                },
                { data: 'dateTime', title: 'Date & Time' },
                {
                    data: 'action',
                    title: 'Action',
                    render: function (data, type) {
                        if (type !== 'display') return data || '';
                        return escapeHtml(data || '');
                    }
                },
                {
                    data: 'category',
                    title: 'Category',
                    render: function (data, type) {
                        if (type !== 'display') return data || '';
                        return escapeHtml(data || '');
                    }
                },
                {
                    data: 'summary',
                    title: 'Summary',
                    render: function (data, type) {
                        if (type !== 'display') return data || '';
                        return escapeHtml(data || '');
                    }
                }
            ],
            ordering: true,
            order: [[2, 'desc']],
            pageLength: 10,
            lengthChange: true,
            language: {
                emptyTable: 'No audit trails found.'
            }
        });
    }

    async function searchAuditTrails() {
        msgEl = document.getElementById('auditTrailsMessage');
        if (msgEl) {
            msgEl.textContent = '';
            msgEl.classList.remove('text-success');
            msgEl.classList.add('text-danger');
        }

        const payload = buildSearchPayload();

        try {
            const response = await fetch('/AuditTrails/Search', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                if (msgEl) msgEl.textContent = 'Server error while searching audit trails.';
                return;
            }

            const result = await response.json();
            if (!result.success) {
                if (msgEl) msgEl.textContent = result.message || 'Failed to search audit trails.';
                return;
            }

            const rows = result.data || [];

            if (msgEl) {
                msgEl.classList.remove('text-danger');
                msgEl.classList.add('text-muted');
                msgEl.textContent = rows.length === 0
                    ? 'No audit trails matched your filters.'
                    : `Found ${rows.length} audit trail(s).`;
            }

            initOrUpdateDataTable(rows);
        } catch (err) {
            console.error('Error searching audit trails', err);
            if (msgEl) msgEl.textContent = 'An unexpected error occurred while searching audit trails.';
        }
    }

    function clearFilters() {
        const fromDate = document.getElementById('filterFromDate');
        const toDate = document.getElementById('filterToDate');
        if (fromDate) fromDate.value = '';
        if (toDate) toDate.value = '';

        const selectIds = ['filterUserId', 'filterAction', 'filterCategory'];
        selectIds.forEach(id => {
            const el = document.getElementById(id);
            if (!el) return;
            el.value = '';

            if (window.jQuery && $.fn.select2 && $(el).hasClass('select2')) {
                $(el).val('').trigger('change');
            }
        });

        msgEl = document.getElementById('auditTrailsMessage');
        if (msgEl) {
            msgEl.textContent = '';
            msgEl.classList.remove('text-danger', 'text-success', 'text-muted');
        }

        if (dt) {
            dt.clear();
            dt.draw();
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        msgEl = document.getElementById('auditTrailsMessage');

        loadLookups();

        const btnSearch = document.getElementById('btnSearchAuditTrails');
        if (btnSearch) btnSearch.addEventListener('click', searchAuditTrails);

        const btnClear = document.getElementById('btnClearAuditTrails');
        if (btnClear) btnClear.addEventListener('click', clearFilters);
    });
})();