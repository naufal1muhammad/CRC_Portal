// @ts-nocheck
(function () {
    let dt = null;

    let currentAppointmentId = 0;
    let modalInstance = null;

    function getPatientId() {
        const root = document.querySelector('[data-patient-id]');
        return root ? (root.getAttribute('data-patient-id') || '') : '';
    }

    function qs(id) {
        return document.getElementById(id);
    }

    function escapeHtml(str) {
        return (str ?? '').toString().replace(/[&<>"']/g, s => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[s]));
    }

    function setMsg(text) {
        const el = qs('appointmentModalMessage');
        if (el) el.textContent = text || '';
    }

    function getModal() {
        const el = qs('appointmentModal');
        if (!el) return null;
        if (!modalInstance) modalInstance = bootstrap.Modal.getOrCreateInstance(el);
        return modalInstance;
    }

    function renderSelectOptions(selectEl, items, valueKey, textKey, placeholder) {
        if (!selectEl) return;
        const ph = placeholder || '-- Select --';
        selectEl.innerHTML = `<option value="">${escapeHtml(ph)}</option>`;
        (items || []).forEach(it => {
            const val = (it[valueKey] ?? '').toString();
            const txt = (it[textKey] ?? '').toString();
            if (!val) return;
            const opt = document.createElement('option');
            opt.value = val;
            opt.textContent = txt;
            selectEl.appendChild(opt);
        });
    }

    async function loadLookups() {
        // Types + Branches
        const res1 = await fetch('/Patient/GetAppointmentLookups', { headers: { 'Accept': 'application/json' } });
        const json1 = await res1.json();

        if (!json1 || !json1.success) {
            console.error('Failed to load appointment lookups');
            return;
        }

        const typeEl = qs('AppointmentType');
        const branchEl = qs('AppointmentBranch');

        const types = (json1.types || []).map(x => ({
            id: x.id || '',
            name: x.name || ''
        }));

        const branches = (json1.branches || []).map(x => ({
            branchId: x.branchId || '',
            branchName: x.branchState ? `${x.branchName} (${x.branchState})` : (x.branchName || '')
        }));

        renderSelectOptions(typeEl, types, 'id', 'name', '-- Select Type --');
        renderSelectOptions(branchEl, branches, 'branchId', 'branchName', '-- Select Location --');

        // Staff list
        const res2 = await fetch('/Patient/GetAppointmentStaffList', { headers: { 'Accept': 'application/json' } });
        const json2 = await res2.json();

        if (!json2 || !json2.success) {
            console.error('Failed to load staff list');
            return;
        }

        const staffEl = qs('AppointmentStaff');
        const staff = (json2.data || []).map(x => ({
            staffId: x.staffId || '',
            staffName: x.staffName || ''
        }));

        renderSelectOptions(staffEl, staff, 'staffId', 'staffName', '-- Select Staff --');
    }

    async function loadAppointments() {
        const patientId = getPatientId();
        if (!patientId) return;

        const res = await fetch(`/Patient/GetAppointments?patientId=${encodeURIComponent(patientId)}`, {
            headers: { 'Accept': 'application/json' }
        });

        const json = await res.json();
        if (!json || !json.success) {
            console.error(json?.message || 'Failed to load appointments');
            return;
        }

        renderAppointments(json.data || []);
    }

    function statusBadge(status) {
        const s = (status || '').toString().trim();
        if (!s) return '';
        const lower = s.toLowerCase();
        let cls = 'badge bg-secondary';
        if (lower === 'scheduled') cls = 'badge rounded-pill bg-warning';
        if (lower === 'attended') cls = 'badge rounded-pill bg-success';
        if (lower === 'not attended') cls = 'badge rounded-pill bg-danger';
        return `<span class="${cls}">${escapeHtml(s)}</span>`;
    }

    function renderAppointments(list) {
        const table = qs('appointmentTable');
        if (!table) return;

        const tbody = table.querySelector('tbody');
        if (!tbody) return;

        tbody.innerHTML = '';

        if (!list || list.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="7" class="text-center text-muted">
                        No appointments found.
                    </td>
                </tr>`;
            return;
        }

        list.forEach(a => {
            const tr = document.createElement('tr');

            const id = a.appointmentId || 0;

            tr.setAttribute('data-id', id);
            tr.setAttribute('data-date-raw', a.appointmentDateRaw || '');
            tr.setAttribute('data-staff-id', a.staffId || '');
            tr.setAttribute('data-type-id', a.typeId || '');
            tr.setAttribute('data-branch-id', a.branchId || '');
            tr.setAttribute('data-status', a.status || '');

            tr.innerHTML = `
                <td>${escapeHtml(a.appointmentDate || '')}</td>
                <td>${escapeHtml(a.from || '')}</td>
                <td>${escapeHtml(a.to || '')}</td>
                <td>${escapeHtml(a.typeName || '')}</td>
                <td>${statusBadge(a.status)}</td>
                <td>${escapeHtml(a.staffName || '')}</td>
                <td>
                    <button type="button" class="btn btn-sm btn-secondary me-1 btn-edit-appointment" title="Edit"><i class="fas fa-edit"></i></button>
                    <button type="button" class="btn btn-sm btn-danger btn-delete-appointment" title="Delete"><i class="fas fa-trash"></i></button>
                </td>
            `;

            tbody.appendChild(tr);
        });
    }

    function resetSlotsTable(message) {
        const body = qs('appointmentSlotsBody');
        if (!body) return;
        body.innerHTML = `
            <tr>
                <td colspan="3" class="text-center text-muted">
                    ${escapeHtml(message || 'Select appointment date and staff to view slots.')}
                </td>
            </tr>
        `;
    }

    async function loadSlotsIfReady() {
        const date = (qs('AppointmentDate')?.value || '').trim();
        const staffId = (qs('AppointmentStaff')?.value || '').trim();

        if (!date || !staffId) {
            resetSlotsTable('Select appointment date and staff to view slots.');
            return;
        }

        resetSlotsTable('Loading slots...');

        const url = `/Patient/GetAppointmentSlots?staffId=${encodeURIComponent(staffId)}&date=${encodeURIComponent(date)}&appointmentId=${encodeURIComponent(currentAppointmentId || 0)}`;
        const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
        const json = await res.json();

        if (!json || !json.success) {
            resetSlotsTable(json?.message || 'Failed to load slots.');
            return;
        }

        renderSlots(json.data || []);
    }

    function renderSlots(slots) {
        const body = qs('appointmentSlotsBody');
        if (!body) return;

        body.innerHTML = '';

        if (!slots || slots.length === 0) {
            resetSlotsTable('No slots found for the selected staff/date.');
            return;
        }

        slots.forEach(s => {
            const slotId = s.staffSlotId || 0;
            const start = (s.slotStartTime || '').toString();
            const end = (s.slotEndTime || '').toString();
            const paId = s.patientAppointmentId;

            const isSelf = paId && (parseInt(paId, 10) === parseInt(currentAppointmentId || 0, 10));
            const isBooked = paId && !isSelf;

            const statusText = isBooked ? 'Not Available' : 'Available';
            const statusBadgeHtml = isBooked
                ? '<span class="badge bg-danger">Not Available</span>'
                : '<span class="badge bg-success">Available</span>';

            const disabledAttr = isBooked ? 'disabled' : '';
            const checkedAttr = isSelf ? 'checked' : '';

            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>${escapeHtml(`${start} - ${end}`)}</td>
                <td>${statusBadgeHtml}</td>
                <td class="text-center">
                    <input type="checkbox" class="form-check-input slot-check"
                           value="${escapeHtml(slotId)}"
                           ${disabledAttr} ${checkedAttr} />
                </td>
            `;
            body.appendChild(tr);
        });
    }

    function openAddModal() {
        currentAppointmentId = 0;
        setMsg('');

        const title = document.querySelector('#appointmentModal .modal-title');
        if (title) title.textContent = 'Add Appointment';

        if (qs('AppointmentDate')) qs('AppointmentDate').value = '';
        if (qs('AppointmentStaff')) qs('AppointmentStaff').value = '';
        if (qs('AppointmentType')) qs('AppointmentType').value = '';
        if (qs('AppointmentBranch')) qs('AppointmentBranch').value = '';
        if (qs('AppointmentStatus')) qs('AppointmentStatus').value = 'Scheduled';

        resetSlotsTable('Select appointment date and staff to view slots.');

        getModal()?.show();
    }

    async function openEditModal(row) {
        if (!row) return;

        currentAppointmentId = parseInt(row.getAttribute('data-id') || '0', 10) || 0;
        setMsg('');

        const title = document.querySelector('#appointmentModal .modal-title');
        if (title) title.textContent = 'Edit Appointment';

        const dateRaw = row.getAttribute('data-date-raw') || '';
        const staffId = row.getAttribute('data-staff-id') || '';
        const typeId = row.getAttribute('data-type-id') || '';
        const branchId = row.getAttribute('data-branch-id') || '';
        const status = row.getAttribute('data-status') || 'Scheduled';

        if (qs('AppointmentDate')) qs('AppointmentDate').value = dateRaw;
        if (qs('AppointmentStaff')) qs('AppointmentStaff').value = staffId;
        if (qs('AppointmentType')) qs('AppointmentType').value = typeId;
        if (qs('AppointmentBranch')) qs('AppointmentBranch').value = branchId;
        if (qs('AppointmentStatus')) qs('AppointmentStatus').value = status;

        resetSlotsTable('Loading slots...');

        getModal()?.show();

        await loadSlotsIfReady();
    }

    async function saveAppointment() {
        setMsg('');

        const patientId = getPatientId();
        const date = (qs('AppointmentDate')?.value || '').trim();
        const staffId = (qs('AppointmentStaff')?.value || '').trim();
        const typeId = (qs('AppointmentType')?.value || '').trim();
        const branchId = (qs('AppointmentBranch')?.value || '').trim();
        const status = (qs('AppointmentStatus')?.value || '').trim();

        const checked = Array.from(document.querySelectorAll('#appointmentSlotsBody .slot-check:checked'));
        const slotIds = checked
            .map(x => parseInt(x.value, 10))
            .filter(x => !isNaN(x) && x > 0);

        if (!date) return setMsg('Appointment Date is required.');
        if (!staffId) return setMsg('Assigned Staff is required.');
        if (!slotIds.length) return setMsg('Please tick at least one available slot.');
        if (!typeId) return setMsg('Appointment Type is required.');
        if (!branchId) return setMsg('Location is required.');
        if (!status) return setMsg('Attendance Status is required.');

        const payload = {
            appointmentId: currentAppointmentId > 0 ? currentAppointmentId : null,
            patientId,
            appointmentDate: date,
            staffId,
            slotIds,
            pjAppTypeId: typeId,
            branchId,
            status
        };

        const res = await fetch('/Patient/SaveAppointment', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        const json = await res.json();
        if (!json || !json.success) {
            return setMsg(json?.message || 'Error saving appointment.');
        }

        getModal()?.hide();
        await loadAppointments();
    }

    async function deleteAppointment(appointmentId) {
        if (!appointmentId) return;

        const ok = confirm('Delete this appointment?');
        if (!ok) return;

        const res = await fetch('/Patient/DeleteAppointment', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ appointmentId })
        });

        const json = await res.json();
        if (!json || !json.success) {
            alert(json?.message || 'Error deleting appointment.');
            return;
        }

        await loadAppointments();
    }

    function bindEvents() {
        qs('btnAddAppointment')?.addEventListener('click', openAddModal);

        qs('AppointmentDate')?.addEventListener('change', loadSlotsIfReady);
        qs('AppointmentStaff')?.addEventListener('change', loadSlotsIfReady);

        qs('btnSaveAppointment')?.addEventListener('click', saveAppointment);

        // Delegated actions in table
        qs('appointmentTable')?.addEventListener('click', async (e) => {
            const t = e.target;
            if (!t) return;

            const row = t.closest('tr');
            if (!row) return;

            if (t.classList.contains('btn-edit-appointment')) {
                await openEditModal(row);
            }

            if (t.classList.contains('btn-delete-appointment')) {
                const id = parseInt(row.getAttribute('data-id') || '0', 10) || 0;
                await deleteAppointment(id);
            }
        });
    }

    async function init() {
        try {
            await loadLookups();
            await loadAppointments();
            bindEvents();
        } catch (err) {
            console.error(err);
        }
    }

    document.addEventListener('DOMContentLoaded', init);
})();