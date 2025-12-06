// @ts-nocheck
(function() {
    let tableBody;
    let txtSearch;

    function applyActiveSearchFilter() {
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

    async function loadActivePatients() {
        if (!tableBody) return;

        tableBody.innerHTML = `
            <tr>
                <td colspan="8" class="text-center text-muted">Loading...</td>
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
                        <td colspan="8" class="text-center text-danger">Error loading patients.</td>
                    </tr>
                `;
                return;
            }

            const data = await response.json();

            if (!data || !Array.isArray(data) || data.length === 0) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="8" class="text-center text-muted">No active patients found.</td>
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
                    <td>${p.admittedOn || ''}</td>
                    <td>${p.dischargeTypeName || ''}</td>
                    <td>
                        <button type="button"
                                class="btn btn-sm btn-secondary btn-patient-edit"
                                data-id="${p.patientId || ''}"
                                title="Edit">
                            <i class="fas fa-edit"></i>
                        </button>
                    </td>
                `;

                tableBody.appendChild(tr);
            });

            applyActiveSearchFilter();
        } catch (err) {
            console.error('Error loading active patients', err);
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

        if (txtSearch) {
            txtSearch.addEventListener('input', applyActiveSearchFilter);
        }

        attachRowHandlers();
        loadActivePatients();
    });
})();