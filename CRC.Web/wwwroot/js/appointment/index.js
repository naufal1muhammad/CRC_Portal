// @ts-nocheck
(function() {
    let dt = null;
    let msgEl;

    function initSelect2() {
        if (window.jQuery && $.fn.select2) {
            $('.select2').select2({
                width: '100%',
                placeholder: '-- All --',
                allowClear: true
            });
        }
    }

    function setSelectOptions(selectEl, items, placeholderText) {
        if (!selectEl) return;

        // Clear existing
        while (selectEl.firstChild) {
            selectEl.removeChild(selectEl.firstChild);
        }

        // Placeholder
        const placeholder = document.createElement('option');
        placeholder.value = '';
        placeholder.textContent = placeholderText || '-- All --';
        selectEl.appendChild(placeholder);

        (items || []).forEach(i => {
            const value = i.name || '';
            if (!value) return;
            const opt = document.createElement('option');
            opt.value = value;
            opt.textContent = value;
            selectEl.appendChild(opt);
        });
    }

    async function loadLookups() {
        msgEl = document.getElementById('appointmentsMessage');
        if (msgEl) {
            msgEl.textContent = '';
            msgEl.classList.remove('text-success');
            msgEl.classList.add('text-danger');
        }

        try {
            const response = await fetch('/Appointment/GetLookups', {
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

            const patients = result.patients || [];
            const staff = result.staff || [];
            const statuses = result.statuses || [];
            const types = result.types || [];
            const branches = result.branches || [];

            setSelectOptions(document.getElementById('filterPatientName'), patients, '-- All Patients --');
            setSelectOptions(document.getElementById('filterStaffName'), staff, '-- All Staff --');
            setSelectOptions(document.getElementById('filterStatus'), statuses, '-- All Statuses --');
            setSelectOptions(document.getElementById('filterAppointmentType'), types, '-- All Types --');
            setSelectOptions(document.getElementById('filterBranch'), branches, '-- All Branches --');

            initSelect2();

            if (msgEl) {
                msgEl.textContent = '';
                msgEl.classList.remove('text-danger');
            }
        } catch (err) {
            console.error('Error loading appointment lookups', err);
            if (msgEl) msgEl.textContent = 'Error loading filters.';
        }
    }

    function buildSearchPayload() {
        const patientName = (document.getElementById('filterPatientName')?.value || '').trim();
        const staffName = (document.getElementById('filterStaffName')?.value || '').trim();
        const status = (document.getElementById('filterStatus')?.value || '').trim();
        const fromDate = document.getElementById('filterFromDate')?.value || '';
        const toDate = document.getElementById('filterToDate')?.value || '';
        const typeName = (document.getElementById('filterAppointmentType')?.value || '').trim();
        const branchName = (document.getElementById('filterBranch')?.value || '').trim();

        return {
            patientName: patientName || null,
            staffName: staffName || null,
            status: status || null,
            fromDate: fromDate || null,
            toDate: toDate || null,
            pjAppTypeName: typeName || null,
            branchName: branchName || null
        };
    }

    function escapeHtml(str) {
  return (str ?? '').toString().replace(/[&<>"']/g, s => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  }[s]));
}

function getNextStatus(current) {
  const v = (current || '').toString().trim().toLowerCase();
  if (v === 'scheduled') return 'Attended';
  if (v === 'attended') return 'Not Attended';
  if (v === 'not attended') return 'Scheduled';
  return 'Attended';
}

async function updateAttendanceStatus(appointmentId, newStatus) {
  const response = await fetch('/Appointment/UpdateAppointmentStatus', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json'
    },
    body: JSON.stringify({ patientAppointmentId: appointmentId, status: newStatus })
  });

  if (!response.ok) throw new Error('Server error updating status.');

  const result = await response.json();
  if (!result.success) throw new Error(result.message || 'Failed to update status.');
}


    function initOrUpdateDataTable(rows) {
        const $table = $('#appointmentsTable');

        if (dt) {
            dt.clear();
            dt.rows.add(rows);
            dt.draw();
            return;
        }

        dt = $table.DataTable({
            data: rows,
            columns: [
                { data: 'patientId', title: 'Patient ID' },
                {
  data: 'patientName',
  title: 'Patient Name',
  render: function (data, type, row) {
    if (type !== 'display') return data || '';
    const id = row.patientId || '';
    const name = escapeHtml(data || '');
    // If you created a proxy endpoint in Step 2, use it here instead
    // e.g. `/Appointment/OpenPatient?patientId=...`
    return `<a href="/Patient/Edit/${encodeURIComponent(id)}" class="text-decoration-none">${name}</a>`;
  }
},
                { data: 'patientPhone', title: 'Phone' },
                { data: 'patientEmail', title: 'Email' },
                { data: 'appointmentType', title: 'Appointment Type' },
                {
  data: 'status',
  title: 'Attendance Status',
  render: function (data, type, row) {
    if (type !== 'display') return data || '';

    const raw = (data || '').toString();
    const val = raw.trim().toLowerCase();

    let badgeClass = 'badge rounded-pill bg-warning';
    if (val === 'attended') badgeClass = 'badge rounded-pill bg-success';
    else if (val === 'not attended') badgeClass = 'badge rounded-pill bg-danger';
    else badgeClass = 'badge rounded-pill bg-warning';

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
            // Keep layout clean, use default search box + paging
            language: {
                emptyTable: 'No appointments found.'
            }
        });
        $('#appointmentsTable').off('click', '.js-status-toggle');
$('#appointmentsTable').on('click', '.js-status-toggle', async function (e) {
  e.preventDefault();
  e.stopPropagation();

  if (!dt) return;

  let $tr = $(this).closest('tr');
  if ($tr.hasClass('child')) $tr = $tr.prev(); // responsive fix

  const rowApi = dt.row($tr);
  const rowData = rowApi.data();
  if (!rowData) return;

  const apptId = rowData.patientAppointmentId || rowData.appointmentId || 0;
  if (!apptId) return;

  const next = getNextStatus(rowData.status);

  try {
    await updateAttendanceStatus(apptId, next);

    // update UI immediately
    rowData.status = next;
    rowApi.data(rowData).invalidate().draw(false);
  } catch (err) {
    alert(err.message || 'Failed to update status.');
  }
});
    }

    async function searchAppointments() {
        msgEl = document.getElementById('appointmentsMessage');
        if (msgEl) {
            msgEl.textContent = '';
            msgEl.classList.remove('text-success');
            msgEl.classList.add('text-danger');
        }

        const payload = buildSearchPayload();

        try {
            const response = await fetch('/Appointment/Search', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                if (msgEl) msgEl.textContent = 'Server error while searching appointments.';
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (msgEl) msgEl.textContent = result.message || 'Failed to search appointments.';
                return;
            }

            const rows = result.data || [];

            if (msgEl) {
                msgEl.classList.remove('text-danger');
                msgEl.classList.add('text-muted');
                msgEl.textContent = rows.length === 0
                    ? 'No appointments matched your filters.'
                    : `Found ${rows.length} appointment(s).`;
            }

            initOrUpdateDataTable(rows);
        } catch (err) {
            console.error('Error searching appointments', err);
            if (msgEl) msgEl.textContent = 'An unexpected error occurred while searching appointments.';
        }
    }

    function clearAppointmentFilters() {
    // Text / date filters
    const fromDate = document.getElementById('filterFromDate');
    const toDate = document.getElementById('filterToDate');

    if (fromDate) fromDate.value = '';
    if (toDate) toDate.value = '';

    // Select filters
    const selectIds = [
        'filterPatientName',
        'filterStaffName',
        'filterStatus',
        'filterAppointmentType',
        'filterBranch'
    ];

    selectIds.forEach(id => {
        const el = document.getElementById(id);
        if (!el) return;

        el.value = '';

        // If using Select2, also reset its UI
        if (window.jQuery && $.fn.select2 && $(el).hasClass('select2')) {
            $(el).val('').trigger('change');
        }
    });

    // Clear message
    msgEl = document.getElementById('appointmentsMessage');
    if (msgEl) {
        msgEl.textContent = '';
        msgEl.classList.remove('text-danger', 'text-success', 'text-muted');
    }

    // Clear the DataTable rows (so page becomes "empty" again)
    if (dt) {
        dt.clear();
        dt.draw();
    }
}

    document.addEventListener('DOMContentLoaded', function() {
        msgEl = document.getElementById('appointmentsMessage');

        loadLookups();

        const btnSearch = document.getElementById('btnSearchAppointments');
        if (btnSearch) {
            btnSearch.addEventListener('click', searchAppointments);
        }

        const btnClear = document.getElementById('btnClearAppointments');
    if (btnClear) {
        btnClear.addEventListener('click', clearAppointmentFilters);
    }
    });
})();