// @ts-nocheck
(function() {
    let tableBody;
    let msg;
    let btnAdd;
    let modalEl;
    let modal;
    let txtDateTime;
    let selType;
    let selStatus;
    let modalMsg;
    let modalTitle;

    // Track whether we're adding or editing
    let currentAppointmentId = null;

    function getPatientId() {
        const root = document.querySelector('[data-patient-id]');
        return root ? (root.getAttribute('data-patient-id') || '') : '';
    }

    async function loadAppointmentTypes() {
        if (!selType) return;

        selType.innerHTML = '<option value="">Loading types...</option>';

        try {
            const response = await fetch('/Patient/GetAppointmentLookups', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                selType.innerHTML = '<option value="">Error loading types</option>';
                return;
            }

            const result = await response.json();
            if (!result.success) {
                selType.innerHTML = '<option value="">Error loading types</option>';
                return;
            }

            const types = result.types || [];
            selType.innerHTML = '<option value="">-- Select Type --</option>';

            types.forEach(t => {
                const opt = document.createElement('option');
                opt.value = t.name || '';
                opt.textContent = t.name || '';
                selType.appendChild(opt);
            });
        } catch (err) {
            console.error('Error loading appointment types', err);
            selType.innerHTML = '<option value="">Error loading types</option>';
        }
    }

    async function loadAppointments() {
        if (!tableBody) return;

        const patientId = getPatientId();
        if (!patientId) {
            tableBody.innerHTML = `
                <tr>
                    <td colspan="4" class="text-center text-muted">
                        Save Basic Details first before adding appointments.
                    </td>
                </tr>
            `;
            if (btnAdd) btnAdd.disabled = true;
            return;
        }

        if (btnAdd) btnAdd.disabled = false;

        tableBody.innerHTML = `
            <tr>
                <td colspan="4" class="text-center text-muted">Loading...</td>
            </tr>
        `;

        try {
            const response = await fetch('/Patient/GetAppointments?patientId=' + encodeURIComponent(patientId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="4" class="text-center text-danger">Error loading appointments.</td>
                    </tr>
                `;
                return;
            }

            const result = await response.json();
            if (!result.success) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="4" class="text-center text-danger">
                            ${result.message || 'Error loading appointments.'}
                        </td>
                    </tr>
                `;
                return;
            }

            const data = result.data || [];

            if (data.length === 0) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="4" class="text-center text-muted">No appointments found.</td>
                    </tr>
                `;
                return;
            }

            tableBody.innerHTML = '';

            data.forEach(a => {
                const tr = document.createElement('tr');
                tr.setAttribute('data-id', a.appointmentId || 0);
                tr.setAttribute('data-datetime-raw', a.appointmentDateTimeRaw || '');
                tr.setAttribute('data-type-name', a.typeName || '');
                tr.setAttribute('data-status', a.status || '');

                tr.innerHTML = `
                    <td>${a.appointmentDateTime || ''}</td>
                    <td>${a.typeName || ''}</td>
                    <td>${a.status || ''}</td>
                    <td>
                        <button type="button"
                                class="btn btn-sm btn-secondary btn-app-edit"
                                data-id="${a.appointmentId || 0}"
                                title="Edit">
                            <i class="fas fa-edit"></i>
                        </button>
                        <button type="button"
                                class="btn btn-sm btn-danger btn-app-delete ms-1"
                                data-id="${a.appointmentId || 0}"
                                title="Delete">
                            <i class="fas fa-trash"></i>
                        </button>
                    </td>
                `;

                tableBody.appendChild(tr);
            });
        } catch (err) {
            console.error('Error loading appointments', err);
            tableBody.innerHTML = `
                <tr>
                    <td colspan="4" class="text-center text-danger">Error loading appointments.</td>
                </tr>
            `;
        }
    }

    function openAddAppointmentModal() {
        const patientId = getPatientId();
        if (!patientId) {
            alert('Please save Basic Details first before adding appointments.');
            return;
        }

        currentAppointmentId = null; // NEW

        if (modalMsg) {
            modalMsg.textContent = '';
            modalMsg.classList.remove('text-success');
            modalMsg.classList.add('text-danger');
        }

        if (modalTitle) {
            modalTitle.textContent = 'Add Appointment';
        }

        if (txtDateTime) txtDateTime.value = '';
        if (selType) selType.value = '';
        if (selStatus) selStatus.value = '';

        if (modal) modal.show();
    }

    function openEditAppointmentModal(appointmentId, rowEl) {
        const patientId = getPatientId();
        if (!patientId) {
            alert('Please save Basic Details first.');
            return;
        }

        currentAppointmentId = appointmentId;

        if (modalMsg) {
            modalMsg.textContent = '';
            modalMsg.classList.remove('text-success');
            modalMsg.classList.add('text-danger');
        }

        if (modalTitle) {
            modalTitle.textContent = 'Edit Appointment';
        }

        const rawDateTime = rowEl.getAttribute('data-datetime-raw') || '';
        const typeName = rowEl.getAttribute('data-type-name') || '';
        const status = rowEl.getAttribute('data-status') || '';

        if (txtDateTime) txtDateTime.value = rawDateTime;
        if (selType) selType.value = typeName;
        if (selStatus) selStatus.value = status;

        if (modal) modal.show();
    }

    async function saveAppointment() {
        const patientId = getPatientId();
        if (!patientId) {
            if (modalMsg) modalMsg.textContent = 'Please save Basic Details first.';
            return;
        }

        if (modalMsg) {
            modalMsg.textContent = '';
            modalMsg.classList.remove('text-success');
            modalMsg.classList.add('text-danger');
        }

        const dateTimeVal = txtDateTime ? txtDateTime.value : '';
        const typeVal = selType ? selType.value : '';
        const statusVal = selStatus ? selStatus.value : '';

        if (!dateTimeVal || !typeVal || !statusVal) {
            if (modalMsg) modalMsg.textContent = 'Please fill in all mandatory appointment fields.';
            return;
        }

        const payload = {
            appointmentId: currentAppointmentId,      // <-- NEW
            patientId: patientId,
            pjAppTypeName: typeVal,
            appointmentDateTime: dateTimeVal,
            status: statusVal
        };

        try {
            const response = await fetch('/Patient/SaveAppointment', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                if (modalMsg) modalMsg.textContent = 'Server error while saving appointment.';
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (modalMsg) modalMsg.textContent = result.message || 'Failed to save appointment.';
                return;
            }

            if (modalMsg) {
                modalMsg.classList.remove('text-danger');
                modalMsg.classList.add('text-success');
                modalMsg.textContent = 'Appointment saved.';
            }

            if (modal) modal.hide();

            await loadAppointments();
        } catch (err) {
            console.error('Error saving appointment', err);
            if (modalMsg) modalMsg.textContent = 'An unexpected error occurred while saving appointment.';
        }
    }

    async function deleteAppointment(appointmentId) {
        if (!appointmentId || appointmentId <= 0) return;
        if (!confirm('Are you sure you want to delete this appointment?')) return;

        try {
            const response = await fetch('/Patient/DeleteAppointment', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ appointmentId: appointmentId })
            });

            if (!response.ok) {
                alert('Server error while deleting appointment.');
                return;
            }

            const result = await response.json();

            if (!result.success) {
                alert(result.message || 'Failed to delete appointment.');
                return;
            }

            await loadAppointments();
        } catch (err) {
            console.error('Error deleting appointment', err);
            alert('An unexpected error occurred while deleting appointment.');
        }
    }

    function attachRowHandlers() {
        document.addEventListener('click', function(e) {
            const editBtn = e.target.closest('.btn-app-edit');
            if (editBtn) {
                const idStr = editBtn.getAttribute('data-id');
                const id = idStr ? parseInt(idStr, 10) : 0;
                if (id > 0) {
                    const row = editBtn.closest('tr');
                    if (row) {
                        openEditAppointmentModal(id, row);
                    }
                }
            }

            const delBtn = e.target.closest('.btn-app-delete');
            if (delBtn) {
                const idStr = delBtn.getAttribute('data-id');
                const id = idStr ? parseInt(idStr, 10) : 0;
                if (id > 0) {
                    deleteAppointment(id);
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function() {
        tableBody = document.querySelector('#appointmentTable tbody');
        msg = document.getElementById('appointmentMessage');
        btnAdd = document.getElementById('btnAddAppointment');
        modalEl = document.getElementById('appointmentModal');
        modal = modalEl ? new bootstrap.Modal(modalEl) : null;
        txtDateTime = document.getElementById('AppointmentDateTime');
        selType = document.getElementById('AppointmentType');
        selStatus = document.getElementById('AppointmentStatus');
        modalMsg = document.getElementById('appointmentModalMessage');
        modalTitle = modalEl ? modalEl.querySelector('.modal-title') : null;

        attachRowHandlers();
        loadAppointmentTypes();
        loadAppointments();

        if (btnAdd) {
            btnAdd.addEventListener('click', openAddAppointmentModal);
        }

        const btnSave = document.getElementById('btnSaveAppointment');
        if (btnSave) {
            btnSave.addEventListener('click', saveAppointment);
        }
    });
})();