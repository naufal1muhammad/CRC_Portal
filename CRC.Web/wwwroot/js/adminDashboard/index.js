// @ts-nocheck
(function() {
    let dt = null;
    let msgEl = null;
    let ddlBranch = null;

    // ---------------- helpers ----------------

    function setMsg(text, cssClass) {
        if (!msgEl) return;
        msgEl.className = 'small mb-2 ' + (cssClass || 'text-muted');
        msgEl.textContent = text || '';
    }

    function escapeHtml(str) {
        return (str ?? '').toString().replace(/[&<>"']/g, s => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[s]));
    }

    function setBranchOptions(selectEl, items) {
  if (!selectEl) return;

  selectEl.innerHTML = '';

  // All Branches option
  const optAll = document.createElement('option');
  optAll.value = '';
  optAll.textContent = 'All Branches';
  selectEl.appendChild(optAll);

  (items || []).forEach(b => {
    const name = (b?.name || '').toString().trim();
    if (!name) return;

    const opt = document.createElement('option');
    opt.value = name;
    opt.textContent = name;
    selectEl.appendChild(opt);
  });
}

    function getNextStatus(current) {
        const v = (current || '').toString().trim().toLowerCase();
        if (v === 'scheduled') return 'Attended';
        if (v === 'attended') return 'Not Attended';
        if (v === 'not attended') return 'Scheduled';
        return 'Scheduled';
    }

    async function getJson(url) {
        const res = await fetch(url, { method: 'GET', headers: { 'Accept': 'application/json' } });
        if (!res.ok) throw new Error('HTTP ' + res.status);
        return await res.json();
    }

    async function postJson(url, payload) {
        const res = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (!res.ok) throw new Error('HTTP ' + res.status);
        return await res.json();
    }

    // ---------------- API calls ----------------

    async function loadBranches() {
  if (!ddlBranch) return; // ddlBranch = #ddlTodayAppointmentsBranch

  const response = await fetch('/AdminDashboard/GetBranches', {
    method: 'GET',
    headers: { 'Accept': 'application/json' }
  });

  const result = await response.json();

  if (!response.ok || !result.success) {
    setBranchOptions(ddlBranch, []);
    return;
  }

  setBranchOptions(ddlBranch, result.data || []);
}

    async function loadTodayAppointments(branchName) {
        // expects: { success:true, data:[{ patientAppointmentId, patientId, patientName, ... }] }
        try {
            setMsg('Loading today’s appointments...', 'text-muted');

            const url = branchName
                ? '/AdminDashboard/GetTodayAppointments?branchName=' + encodeURIComponent(branchName)
                : '/AdminDashboard/GetTodayAppointments';

            const result = await getJson(url);
            if (!result.success) throw new Error(result.message || 'Failed loading appointments');

            const rows = result.data || [];
            initOrUpdateDataTable(rows);

            setMsg(
                rows.length === 0 ? 'No appointments found for today.' : `Found ${rows.length} appointment(s) for today.`,
                'text-muted'
            );
        } catch (err) {
            console.error('Error loading today appointments', err);
            setMsg('Error loading today’s appointments.', 'text-danger');

            if (dt) {
                dt.clear().draw();
            }
        }
    }

    async function updateAppointmentStatus(patientAppointmentId, newStatus) {
        // expects: { success:true }
        const result = await postJson('/AdminDashboard/UpdateAppointmentStatus', {
            patientAppointmentId: patientAppointmentId,
            status: newStatus
        });

        if (!result.success) throw new Error(result.message || 'Failed to update status');
    }

    // ---------------- DataTable ----------------

    function initOrUpdateDataTable(rows) {
        const $table = $('#todayAppointmentsTable');

        if (dt) {
            dt.clear();
            dt.rows.add(rows);
            dt.draw(false);
            return;
        }

        dt = $table.DataTable({
            data: rows,
            columns: [
                { data: 'patientId', title: 'Patient ID' },
                {
                    data: 'patientName',
                    title: 'Patient Name',
                    render: function(data, type, row) {
                        if (type !== 'display') return data || '';
                        const id = row.patientId || '';
                        const name = escapeHtml(data || '');
                        return `<a href="/Patient/Edit/${encodeURIComponent(id)}" class="text-decoration-none">${name}</a>`;
                    }
                },
                { data: 'patientPhone', title: 'Phone' },
                { data: 'patientEmail', title: 'Email' },
                { data: 'appointmentType', title: 'Appointment Type' },
                {
                    data: 'status',
                    title: 'Attendance Status',
                    render: function(data, type, row) {
                        if (type !== 'display') return data || '';

                        const raw = (data || '').toString();
                        const val = raw.trim().toLowerCase();

                        let badgeClass = 'badge rounded-pill bg-warning';
                        if (val === 'attended') badgeClass = 'badge rounded-pill bg-success';
                        else if (val === 'not attended') badgeClass = 'badge rounded-pill bg-danger';

                        return `
              <a href="#"
                 class="${badgeClass} js-status-toggle"
                 title="Click to toggle"
                 data-appt-id="${row.patientAppointmentId || 0}">
                ${escapeHtml(raw)}
              </a>
            `;
                    }
                },
                { data: 'staffName', title: 'Assigned Staff' },
                { data: 'branchName', title: 'Branch' },
                { data: 'appointmentDateTime', title: 'Date & Time' }
            ],
            ordering: true,
            pageLength: 10,
            lengthChange: true,
            language: { emptyTable: 'No appointments found.' }
        });

        // toggle click handler
        $('#todayAppointmentsTable').off('click', '.js-status-toggle');
        $('#todayAppointmentsTable').on('click', '.js-status-toggle', async function(e) {
            e.preventDefault();
            e.stopPropagation();

            if (!dt) return;

            let $tr = $(this).closest('tr');
            if ($tr.hasClass('child')) $tr = $tr.prev(); // responsive fix

            const rowApi = dt.row($tr);
            const rowData = rowApi.data();
            if (!rowData) return;

            const apptId = rowData.patientAppointmentId || 0;
            if (!apptId) return;

            const next = getNextStatus(rowData.status);

            try {
                await updateAppointmentStatus(apptId, next);

                rowData.status = next;
                rowApi.data(rowData).invalidate().draw(false);
            } catch (err) {
                console.error(err);
                alert(err.message || 'Error updating appointment status.');
            }
        });
    }

    // ---------------- init ----------------

    document.addEventListener('DOMContentLoaded', async function () {
  msgEl = document.getElementById('adminDashboardMessage');
  ddlBranch = document.getElementById('ddlTodayAppointmentsBranch');

  if (!ddlBranch) {
    console.error('ERROR: ddlTodayAppointmentsBranch not found. Check the ID in your .cshtml.');
    return;
  }

  const onBranchChanged = () => {
    const branchName = (ddlBranch.value || '').trim();
    console.log('Branch changed =>', branchName); // remove later
    loadTodayAppointments(branchName);
  };

  // Hard bind (overrides anything else)
  ddlBranch.onchange = onBranchChanged;

  // Extra safety (in case something rebinds)
  ddlBranch.addEventListener('change', onBranchChanged);

  // Event delegation safety net (in case UI plugin triggers change differently)
  document.addEventListener('change', function (e) {
    if (e.target && e.target.id === 'ddlTodayAppointmentsBranch') {
      onBranchChanged();
    }
  });

  await loadBranches();

  // Load initial table (default All Branches)
  onBranchChanged();
});

})();