// @ts-nocheck
(function() {
    let tableBody;
    let txtSearch;
    let dataTable = null;

    function initDataTable(emptyMessage) {
        // Destroy existing instance if any, then re-init
        if ($.fn.dataTable.isDataTable('#dischargedPatientsTable')) {
            $('#dischargedPatientsTable').DataTable().destroy();
        }

        dataTable = $('#dischargedPatientsTable').DataTable({
            paging: true,
            lengthChange: true,
            pageLength: 10,
            order: [], // no initial sort
            language: emptyMessage ? { emptyTable: emptyMessage } : undefined,
        });
    }

    function applyDischargedSearchFilter() {
        if (!txtSearch || !dataTable) return;

        const filter = txtSearch.value || '';
        // Global search across all columns
        dataTable.search(filter).draw();
    }

    async function loadDischargedPatients() {
        if (!tableBody) return;

        tableBody.innerHTML = `
            <tr>
                <td colspan="4" class="text-center text-muted">Loading...</td>
            </tr>
        `;

        try {
            const response = await fetch('/Patient/GetDischargedPatients', {
                method: 'GET',
                headers: { 'Accept': 'application/json' },
                cache: 'no-store'
            });

            if (!response.ok) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="4" class="text-center text-danger">Error loading patients.</td>
                    </tr>
                `;
                return;
            }

            const data = await response.json();

            if (!data || !Array.isArray(data) || data.length === 0) {
                tableBody.innerHTML = '';
                // Still initialise DataTables so the UI (arrows, pager) appears
                initDataTable('No discharged patients found.');
                return;
            }

            tableBody.innerHTML = '';

            data.forEach(p => {
                const tr = document.createElement('tr');
                tr.setAttribute('data-id', p.patientId || '');

                tr.innerHTML = `
                    <td>${p.patientId || ''}</td>
                    <td>${p.name || ''}</td>
                    <td>${p.dischargeDate || ''}</td>
                    <td>
                        <button type="button"
                                class="btn btn-sm btn-secondary btn-patient-edit"
                                data-id="${p.patientId || ''}"
                                title="Edit">
                            <i class="fas fa-edit"></i>
                        </button>
                        <button type="button"
                                class="btn btn-sm btn-danger ms-1 btn-patient-delete"
                                data-id="${p.patientId || ''}"
                                title="Delete">
                            <i class="fas fa-trash"></i>
                        </button>
                    </td>
                `;

                tableBody.appendChild(tr);
            });

            // Init DataTables after rows are in DOM
            initDataTable();
            applyDischargedSearchFilter();
        } catch (err) {
            console.error('Error loading discharged patients', err);
            tableBody.innerHTML = `
                <tr>
                    <td colspan="4" class="text-center text-danger">Error loading patients.</td>
                </tr>
            `;
        }
    }

    function removePatientRow(patientId) {
        if (!tableBody || !dataTable) return;

        const escapedId = window.CSS && CSS.escape ? CSS.escape(patientId) : patientId;
        const row = tableBody.querySelector(`tr[data-id="${escapedId}"]`);

        if (row) {
            dataTable.row(row).remove().draw(false);
        }
    }

    async function deletePatient(patientId) {
        if (!patientId) return;

        if (!confirm('Are you sure you want to delete this patient and ALL related records?')) {
            return;
        }

        try {
            const response = await fetch('/Patient/DeletePatient', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                    'X-CSRF-Token': $('input:hidden[name="__RequestVerificationToken"]').val()
                },
                body: JSON.stringify({ patientId: patientId })
            });

            const result = await response.json();

            if (!response.ok || !result.success) {
                alert(result.message || 'Error deleting patient.');
                return;
            }

            removePatientRow(patientId);
            await loadDischargedPatients();
        } catch (err) {
            console.error('Error deleting patient', err);
            alert('An unexpected error occurred while deleting patient.');
        }
    }

    function attachRowHandlers() {
        document.addEventListener('click', function(e) {
            const editBtn = e.target.closest('.btn-patient-edit');
            if (editBtn) {
                const id = editBtn.getAttribute('data-id');
                if (id) {
                    window.location.href = '/Patient/Edit/' + encodeURIComponent(id);
                }
            }

            const delBtn = e.target.closest('.btn-patient-delete');
            if (delBtn) {
                const id = delBtn.getAttribute('data-id');
                if (id) {
                    deletePatient(id);
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function() {
        tableBody = document.querySelector('#dischargedPatientsTable tbody');
        txtSearch = document.getElementById('dischargedPatientSearch');

        if (txtSearch) {
            // Same as Active: use keyup so DataTables search works smoothly
            txtSearch.addEventListener('keyup', applyDischargedSearchFilter);
        }

        attachRowHandlers();
        loadDischargedPatients();
    });
})();
