// @ts-nocheck
(function() {
    let tableBody;
    let msg;
    let btnAdd;
    let modalEl;
    let modal;
    let txtDate;
    let selType;
    let selStaff;
    let txtRemarks;
    let modalMsg;
    let modalTitle;

    function getPatientId() {
        const root = document.querySelector('[data-patient-id]');
        return root ? (root.getAttribute('data-patient-id') || '') : '';
    }

    async function loadJourneyTypes() {
        if (!selType) return;

        selType.innerHTML = '<option value="">Loading journey types...</option>';

        try {
            // Reuse the same endpoint as Appointment (LU_PJ_APP_TYPE)
            const response = await fetch('/Patient/GetAppointmentLookups', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                selType.innerHTML = '<option value="">Error loading journey types</option>';
                return;
            }

            const result = await response.json();
            if (!result.success) {
                selType.innerHTML = '<option value="">Error loading journey types</option>';
                return;
            }

            const types = result.types || [];
            selType.innerHTML = '<option value="">-- Select Journey Type --</option>';

            types.forEach(t => {
                const opt = document.createElement('option');
                opt.value = t.name || '';
                opt.textContent = t.name || '';
                selType.appendChild(opt);
            });
        } catch (err) {
            console.error('Error loading journey types', err);
            selType.innerHTML = '<option value="">Error loading journey types</option>';
        }
    }

    async function loadJourneyStaff() {
        if (!selStaff) return;

        selStaff.innerHTML = '<option value="">Loading staff...</option>';

        try {
            const response = await fetch('/Patient/GetJourneyStaffList', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                selStaff.innerHTML = '<option value="">Error loading staff</option>';
                return;
            }

            const result = await response.json();
            if (!result.success) {
                selStaff.innerHTML = '<option value="">Error loading staff</option>';
                return;
            }

            const list = result.data || [];
            selStaff.innerHTML = '<option value="">-- Select Staff --</option>';

            list.forEach(s => {
                const opt = document.createElement('option');
                opt.value = s.staffId || '';
                opt.textContent = s.staffName || '';
                selStaff.appendChild(opt);
            });
        } catch (err) {
            console.error('Error loading staff list', err);
            selStaff.innerHTML = '<option value="">Error loading staff</option>';
        }
    }

    async function loadJourneys() {
        if (!tableBody) return;

        const patientId = getPatientId();
        if (!patientId) {
            tableBody.innerHTML = `
                <tr>
                    <td colspan="5" class="text-center text-muted">
                        Save Basic Details first before adding journeys.
                    </td>
                </tr>
            `;
            if (btnAdd) btnAdd.disabled = true;
            return;
        }

        if (btnAdd) btnAdd.disabled = false;

        tableBody.innerHTML = `
            <tr>
                <td colspan="5" class="text-center text-muted">Loading...</td>
            </tr>
        `;

        try {
            const response = await fetch('/Patient/GetJourneys?patientId=' + encodeURIComponent(patientId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="5" class="text-center text-danger">Error loading journeys.</td>
                    </tr>
                `;
                return;
            }

            const result = await response.json();
            if (!result.success) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="5" class="text-center text-danger">
                            ${result.message || 'Error loading journeys.'}
                        </td>
                    </tr>
                `;
                return;
            }

            const data = result.data || [];

            if (data.length === 0) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="5" class="text-center text-muted">No journeys found.</td>
                    </tr>
                `;
                return;
            }

            tableBody.innerHTML = '';

            data.forEach(j => {
                const tr = document.createElement('tr');
                tr.setAttribute('data-id', j.journeyId || 0);

                tr.innerHTML = `
                    <td>${j.journeyDate || ''}</td>
                    <td>${j.typeName || ''}</td>
                    <td>${j.staffName || ''}</td>
                    <td>${j.remarks || ''}</td>
                    <td>
                        <button type="button"
                                class="btn btn-sm btn-danger btn-journey-delete"
                                data-id="${j.journeyId || 0}"
                                title="Delete">
                            <i class="fas fa-trash"></i>
                        </button>
                    </td>
                `;

                tableBody.appendChild(tr);
            });
        } catch (err) {
            console.error('Error loading journeys', err);
            tableBody.innerHTML = `
                <tr>
                    <td colspan="5" class="text-center text-danger">Error loading journeys.</td>
                </tr>
            `;
        }
    }

    function openAddJourneyModal() {
        const patientId = getPatientId();
        if (!patientId) {
            alert('Please save Basic Details first before adding journeys.');
            return;
        }

        if (modalMsg) {
            modalMsg.textContent = '';
            modalMsg.classList.remove('text-success');
            modalMsg.classList.add('text-danger');
        }

        if (modalTitle) {
            modalTitle.textContent = 'Add Journey';
        }

        if (txtDate) txtDate.value = '';
        if (selType) selType.value = '';
        if (selStaff) selStaff.value = '';
        if (txtRemarks) txtRemarks.value = '';

        if (modal) modal.show();
    }

    async function saveJourney() {
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

        const dateVal = txtDate ? txtDate.value : '';
        const typeVal = selType ? selType.value : '';
        const staffVal = selStaff ? selStaff.value : '';
        const remarksVal = txtRemarks ? txtRemarks.value : '';

        if (!dateVal || !typeVal || !staffVal) {
            if (modalMsg) modalMsg.textContent = 'Please fill in all mandatory journey fields.';
            return;
        }

        const payload = {
            patientId: patientId,
            pjAppTypeName: typeVal,
            journeyDate: dateVal,
            staffId: staffVal,
            remarks: remarksVal
        };

        try {
            const response = await fetch('/Patient/SaveJourney', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                if (modalMsg) modalMsg.textContent = 'Server error while saving journey.';
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (modalMsg) modalMsg.textContent = result.message || 'Failed to save journey.';
                return;
            }

            if (modalMsg) {
                modalMsg.classList.remove('text-danger');
                modalMsg.classList.add('text-success');
                modalMsg.textContent = 'Journey saved.';
            }

            if (modal) modal.hide();

            await loadJourneys();
        } catch (err) {
            console.error('Error saving journey', err);
            if (modalMsg) modalMsg.textContent = 'An unexpected error occurred while saving journey.';
        }
    }

    async function deleteJourney(journeyId) {
        if (!journeyId || journeyId <= 0) return;
        if (!confirm('Are you sure you want to delete this journey?')) return;

        try {
            const response = await fetch('/Patient/DeleteJourney', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ journeyId: journeyId })
            });

            if (!response.ok) {
                alert('Server error while deleting journey.');
                return;
            }

            const result = await response.json();

            if (!result.success) {
                alert(result.message || 'Failed to delete journey.');
                return;
            }

            await loadJourneys();
        } catch (err) {
            console.error('Error deleting journey', err);
            alert('An unexpected error occurred while deleting journey.');
        }
    }

    function attachRowHandlers() {
        document.addEventListener('click', function(e) {
            const delBtn = e.target.closest('.btn-journey-delete');
            if (delBtn) {
                const idStr = delBtn.getAttribute('data-id');
                const id = idStr ? parseInt(idStr, 10) : 0;
                if (id > 0) {
                    deleteJourney(id);
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function() {
        tableBody = document.querySelector('#journeyTable tbody');
        msg = document.getElementById('journeyMessage');
        btnAdd = document.getElementById('btnAddJourney');
        modalEl = document.getElementById('journeyModal');
        modal = modalEl ? new bootstrap.Modal(modalEl) : null;
        txtDate = document.getElementById('JourneyDate');
        selType = document.getElementById('JourneyType');
        selStaff = document.getElementById('JourneyStaff');
        txtRemarks = document.getElementById('JourneyRemarks');
        modalMsg = document.getElementById('journeyModalMessage');
        modalTitle = modalEl ? modalEl.querySelector('.modal-title') : null;

        attachRowHandlers();
        loadJourneyTypes();
        loadJourneyStaff();
        loadJourneys();

        if (btnAdd) {
            btnAdd.addEventListener('click', openAddJourneyModal);
        }

        const btnSave = document.getElementById('btnSaveJourney');
        if (btnSave) {
            btnSave.addEventListener('click', saveJourney);
        }
    });
})();