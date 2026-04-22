// @ts-nocheck
(function () {
    let loaded = false;

    function getStaffId() {
        const root = document.querySelector('[data-staff-id]');
        return root ? (root.getAttribute('data-staff-id') || '') : '';
    }

    function qs(id) {
        return document.getElementById(id);
    }

    function escapeHtml(str) {
        return (str ?? '').toString().replace(/[&<>"']/g, s => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[s]));
    }

    function setMessage(text) {
        const el = qs('perfMessage');
        if (!el) return;
        el.textContent = text || '';
    }

    function countCardHtml(title, value) {
        return ''
            + '<div class="card bg-light border-0 shadow-sm h-100">'
            +   '<div class="card-body text-center">'
            +     '<h6 class="text-muted text-uppercase small mb-2">' + escapeHtml(title) + '</h6>'
            +     '<div class="display-6 fw-semibold">' + escapeHtml(value) + '</div>'
            +   '</div>'
            + '</div>';
    }

    function listCardHtml(title, value, subtitle) {
        const sub = subtitle ? '<div class="text-muted small">' + escapeHtml(subtitle) + '</div>' : '';
        return ''
            + '<div class="col-md-4 mb-3">'
            +   '<div class="card bg-light border-0 shadow-sm h-100">'
            +     '<div class="card-body text-center">'
            +       '<h6 class="text-muted text-uppercase small mb-2">' + escapeHtml(title) + '</h6>'
            +       '<div class="display-6 fw-semibold">' + escapeHtml(value) + '</div>'
            +       sub
            +     '</div>'
            +   '</div>'
            + '</div>';
    }

    function renderCountCard(containerId, title, value) {
        const el = qs(containerId);
        if (!el) return;
        el.innerHTML = countCardHtml(title, value);
    }

    function renderListCards(containerId, items, titleFn, valueFn, subtitleFn, emptyText) {
        const el = qs(containerId);
        if (!el) return;

        if (!items || items.length === 0) {
            el.innerHTML = '<div class="col-12"><em class="text-muted">' + escapeHtml(emptyText) + '</em></div>';
            return;
        }

        let html = '';
        for (let i = 0; i < items.length; i++) {
            html += listCardHtml(titleFn(items[i]), valueFn(items[i]), subtitleFn ? subtitleFn(items[i]) : '');
        }
        el.innerHTML = html;
    }

    function renderPlaceholderForNewStaff() {
        const panes = ['perfTotalColonoscopy', 'perfTotalColonoscopyThisMonth'];
        for (let i = 0; i < panes.length; i++) {
            const el = qs(panes[i]);
            if (el) el.innerHTML = '';
        }

        const lists = ['perfHoursByType', 'perfComplications', 'perfAnomalies'];
        for (let i = 0; i < lists.length; i++) {
            const el = qs(lists[i]);
            if (el) el.innerHTML = '';
        }

        setMessage('Please save the staff first before viewing performance data.');
    }

    function renderAll(data) {
        setMessage('');

        renderCountCard('perfTotalColonoscopy', 'Total Colonoscopy', data.totalColonoscopy ?? 0);
        renderCountCard('perfTotalColonoscopyThisMonth', 'Total Colonoscopy This Month', data.totalColonoscopyThisMonth ?? 0);

        renderListCards(
            'perfHoursByType',
            data.hoursByType,
            item => item.pjAppTypeName || item.pjAppTypeId || 'Unknown',
            item => (item.totalHours ?? 0) + ' h',
            null,
            'No attended appointments recorded.'
        );

        renderListCards(
            'perfComplications',
            data.complications,
            item => item.complication || 'Unspecified',
            item => item.total ?? 0,
            null,
            'No complications recorded.'
        );

        renderListCards(
            'perfAnomalies',
            data.anomalies,
            item => item.typeOfAnomaly || 'Unspecified',
            item => item.patientCount ?? 0,
            item => ((item.patientCount ?? 0) === 1 ? '1 patient' : (item.patientCount ?? 0) + ' patients'),
            'No anomalies detected.'
        );
    }

    async function fetchPerformance(staffId) {
        const response = await fetch('/StaffPerformance/Get?staffId=' + encodeURIComponent(staffId), {
            method: 'GET',
            headers: { 'Accept': 'application/json' }
        });

        if (!response.ok) {
            throw new Error('HTTP ' + response.status);
        }

        const result = await response.json();
        if (!result || !result.success) {
            throw new Error((result && result.message) || 'Failed to load performance data.');
        }

        return result.data;
    }

    async function loadOnce() {
        if (loaded) return;

        const staffId = getStaffId();
        if (!staffId) {
            renderPlaceholderForNewStaff();
            return;
        }

        try {
            const data = await fetchPerformance(staffId);
            renderAll(data);
            loaded = true;
        } catch (err) {
            setMessage(err && err.message ? err.message : 'Failed to load performance data.');
            loaded = false;
        }
    }

    document.addEventListener('DOMContentLoaded', function () {
        const perfTabBtn = document.getElementById('tab-performance');
        if (perfTabBtn) {
            perfTabBtn.addEventListener('shown.bs.tab', loadOnce);
        }
    });
})();
