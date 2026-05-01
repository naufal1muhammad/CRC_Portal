// @ts-nocheck
(function() {
    const btnAdd = document.getElementById('btnAddStaff');
    const txtSearch = document.getElementById('staffSearch');
    const tableBody = document.querySelector('#staffTable tbody');

    async function loadStaffList() {
        if (!tableBody) return;

        tableBody.innerHTML = '<tr><td colspan="7">Loading...</td></tr>';

        try {
            const response = await fetch('/Staff/GetStaffList', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                tableBody.innerHTML = '<tr><td colspan="7">Error loading staff.</td></tr>';
                return;
            }

            const data = await response.json();

            if (!data || data.length === 0) {
                tableBody.innerHTML = '<tr><td colspan="7">No staff found.</td></tr>';
                return;
            }

            tableBody.innerHTML = '';

            data.forEach(s => {
                const tr = document.createElement('tr');
                tr.setAttribute('data-id', s.staffId);

                tr.innerHTML = `
                    <td class="text-nowrap">${s.staffId}</td>
                    <td>${s.name ?? ''}</td>
                    <td class="text-nowrap">${s.nric ?? ''}</td>
                    <td class="text-nowrap">${s.phone ?? ''}</td>
                    <td>${s.email ?? ''}</td>
                    <td>${s.staffTypeName ?? s.staffTypeId ?? ''}</td>
                    <td class="text-center text-nowrap">
                        <button type="button"
                                class="btn btn-sm btn-secondary btn-staff-edit"
                                data-id="${s.staffId}"
                                title="Edit">
                                <i class="fas fa-edit"></i>
                        </button>
                        <button type="button"
                                class="btn btn-sm btn-danger btn-staff-delete ms-1"
                                data-id="${s.staffId}"
                                title="Delete">
                                <i class="fas fa-trash"></i>
                        </button>
                    </td>
                `;

                tableBody.appendChild(tr);
            });

            applyStaffSearchFilter();
        } catch (err) {
            console.error(err);
            tableBody.innerHTML = '<tr><td colspan="7">Error loading staff.</td></tr>';
        }
    }

    function applyStaffSearchFilter() {
        if (!txtSearch) return;

        const filter = txtSearch.value.trim().toLowerCase();
        const tbody = document.querySelector('#staffTable tbody');
        if (!tbody) return;

        const rows = tbody.querySelectorAll('tr');

        rows.forEach(row => {
            const cells = row.querySelectorAll('td');
            if (cells.length < 2) {
                return;
            }

            const idText = (cells[0].textContent || '').toLowerCase();
            const nameText = (cells[1].textContent || '').toLowerCase();
            const combined = idText + ' ' + nameText;

            row.style.display = !filter || combined.includes(filter) ? '' : 'none';
        });
    }

    async function deleteStaff(staffId) {
        if (!confirm('Are you sure you want to delete this staff?')) {
            return;
        }

        try {
            const response = await fetch('/Staff/DeleteStaff', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                    'X-CSRF-Token': $('input:hidden[name="__RequestVerificationToken"]').val()
                },
                body: JSON.stringify({ staffId: staffId })
            });

            if (!response.ok) {
                alert('Server error while deleting staff.');
                return;
            }

            const result = await response.json();

            if (!result.success) {
                alert(result.message || 'Failed to delete staff.');
                return;
            }

            loadStaffList();
        } catch (err) {
            console.error(err);
            alert('An unexpected error occurred.');
        }
    }

    function attachRowActionHandlers() {
        document.addEventListener('click', function(e) {
            const target = e.target;

            const editBtn = target.closest('.btn-staff-edit');
            if (editBtn) {
                const id = editBtn.getAttribute('data-id');
                if (id) {
                    window.location.href = '/Staff/Edit/' + encodeURIComponent(id);
                }
            }

            const deleteBtn = target.closest('.btn-staff-delete');
            if (deleteBtn) {
                const id = deleteBtn.getAttribute('data-id');
                if (id) {
                    deleteStaff(id);
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function() {
        loadStaffList();
        attachRowActionHandlers();

        if (btnAdd) {
            btnAdd.addEventListener('click', function() {
                window.location.href = '/Staff/Edit';
            });
        }

        if (txtSearch) {
            txtSearch.addEventListener('input', function() {
                applyStaffSearchFilter();
            });
        }
    });
})();
