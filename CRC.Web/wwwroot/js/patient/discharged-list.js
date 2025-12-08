// @ts-nocheck
(function() {
    let tableBody;
    let txtSearch;

    function applyDischargedSearchFilter() {
        if (!txtSearch || !tableBody) return;

        const filter = (txtSearch.value || '').trim().toLowerCase();
        const rows = tableBody.querySelectorAll('tr[data-id]');

        rows.forEach(row => {
            const cells = row.querySelectorAll('td');
            if (cells.length < 2) return;

            const idText = (cells[0].textContent || '').toLowerCase();
            const nameText = (cells[1].textContent || '').toLowerCase();
            const combined = idText + ' ' + nameText;

            if (!filter || combined.includes(filter)) {
                row.style.display = '';
            } else {
                row.style.display = 'none';
            }
        });
    }

    async function loadDischargedPatients() {
        if (!tableBody) return;

        tableBody.innerHTML = `
            <tr>
                <td colspan="8" class="text-center text-muted">Loading...</td>
            </tr>
        `;

        try {
            const response = await fetch('/Patient/GetDischargedPatients', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="8" class="text-center text-danger">Error loading patients.</td>
                    </tr>
                `;
                return;
            }

            const data = await response.json();

            if (!data || !Array.isArray(data) || data.length === 0) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="8" class="text-center text-muted">No discharged patients found.</td>
                    </tr>
                `;
                return;
            }

            tableBody.innerHTML = '';

            data.forEach(p => {
                const tr = document.createElement('tr');
                tr.setAttribute('data-id', p.patientId || '');

                tr.innerHTML = `
                    <td>${p.patientId || ''}</td>
                    <td>${p.name || ''}</td>
                    <td>${p.email || ''}</td>
                    <td>${p.phone || ''}</td>
                    <td>${p.branchName || ''}</td>
                    <td>${p.dischargeTypeName || ''}</td>
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
                                data-id="${p.patientId}"
                                title="Delete">
                         <i class="fas fa-trash"></i>
                         </button>
                    </td>
                `;

                tableBody.appendChild(tr);
            });

            applyDischargedSearchFilter();
        } catch (err) {
            console.error('Error loading discharged patients', err);
            tableBody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-danger">Error loading patients.</td>
                </tr>
            `;
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

        // Reload the Discharged list after successful delete
        await loadDischargedPatients();
    } catch (err) {
        console.error('Error deleting patient', err);
        alert('An unexpected error occurred while deleting patient.');
    }
}

    document.addEventListener('DOMContentLoaded', function() {
        tableBody = document.querySelector('#dischargedPatientsTable tbody');
        txtSearch = document.getElementById('dischargedPatientSearch');

        if (txtSearch) {
            txtSearch.addEventListener('input', applyDischargedSearchFilter);
        }

        attachRowHandlers();
        loadDischargedPatients();
    });
})();