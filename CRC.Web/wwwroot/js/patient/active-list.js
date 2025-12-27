// @ts-nocheck
(function() {
    let tableBody;
    let txtSearch;
    let dataTable = null;

    function initDataTable() {
        // Destroy existing instance if any
        if ($.fn.dataTable.isDataTable('#activePatientsTable')) {
            $('#activePatientsTable').DataTable().destroy();
        }

        dataTable = $('#activePatientsTable').DataTable({
            paging: true,
            lengthChange: true,
            pageLength: 10,
            order: [],
        });
    }

    function applyActiveSearchFilter() {
        if (!txtSearch || !dataTable) return;

        const filter = txtSearch.value || '';
        dataTable.search(filter).draw();  // global search across ALL columns
    }

    async function loadActivePatients() {
        if (!tableBody) return;

        // Show loading row
        tableBody.innerHTML = `
            <tr>
                <td colspan="5" class="text-center text-muted">Loading...</td>
            </tr>
        `;

        try {
            const response = await fetch('/Patient/GetActivePatients', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="5" class="text-center text-danger">Error loading patients.</td>
                    </tr>
                `;
                return;
            }

            const data = await response.json();

            if (!data || !Array.isArray(data) || data.length === 0) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="5" class="text-center text-muted">No active patients found.</td>
                    </tr>
                `;
                // Initialise empty DataTable so search & paging still render
                initDataTable();
                return;
            }

            // Build rows with the *new* 5 columns
            tableBody.innerHTML = '';

            data.forEach(p => {
                const tr = document.createElement('tr');
                tr.setAttribute('data-id', p.patientId || '');

                tr.innerHTML = `
                    <td>${p.patientId || ''}</td>
                    <td>${p.name || ''}</td>
                    <td>${p.branchName || ''}</td>
                    <td>${p.admittedOn || ''}</td>
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

            // (Re)initialise DataTables AFTER rows are in the DOM
            initDataTable();

            // Also apply current search text (in case user typed before reload)
            applyActiveSearchFilter();
        } catch (err) {
            console.error('Error loading active patients', err);
            tableBody.innerHTML = `
                <tr>
                    <td colspan="5" class="text-center text-danger">Error loading patients.</td>
                </tr>
            `;
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
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ patientId: patientId })
            });

            const result = await response.json();

            if (!response.ok || !result.success) {
                alert(result.message || 'Error deleting patient.');
                return;
            }

            await loadActivePatients();
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
        tableBody = document.querySelector('#activePatientsTable tbody');
        txtSearch = document.getElementById('activePatientSearch');

        const btnAdd = document.getElementById('btnAddPatient');
        if (btnAdd) {
            btnAdd.addEventListener('click', function() {
                window.location.href = '/Patient/Edit';
            });
        }

        // External search box wired to DataTables global search
        if (txtSearch) {
            txtSearch.addEventListener('keyup', applyActiveSearchFilter);
        }

        attachRowHandlers();
        loadActivePatients();
    });
})();