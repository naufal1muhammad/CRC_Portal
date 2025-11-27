// @ts-nocheck
(function () {
    const btnAdd = document.getElementById('btnAddBranch');
    const modalEl = document.getElementById('branchModal');
    const modal = modalEl ? new bootstrap.Modal(modalEl) : null;

    const txtId = document.getElementById('BranchId');
    const txtName = document.getElementById('BranchName');
    const txtLocation = document.getElementById('BranchLocation');
    const txtState = document.getElementById('BranchState');
    const selStatus = document.getElementById('BranchStatus');
    const hiddenIsNew = document.getElementById('BranchIsNew');
    const msg = document.getElementById('branchFormMessage');

    const tableBody = document.querySelector('#branchTable tbody');

    function clearForm() {
        if (!txtId) return;

        txtId.value = '';
        txtName.value = '';
        txtLocation.value = '';
        if (txtState) txtState.value = '';
        if (selStatus) selStatus.value = '1';
        if (hiddenIsNew) hiddenIsNew.value = 'true';
        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-success');
            msg.classList.add('text-danger');
        }
        const label = document.getElementById('branchModalLabel');
        if (label) label.textContent = 'Add Branch';
    }

    function mapStatusText(status) {
        if (status === true || status === 1 || status === '1') return 'Active';
        return 'Inactive';
    }

    async function loadBranches() {
        if (!tableBody) return;

        tableBody.innerHTML = '<tr><td colspan="6">Loading...</td></tr>';

        try {
            const response = await fetch('/Branch/GetBranches', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                tableBody.innerHTML = '<tr><td colspan="6">Error loading branches.</td></tr>';
                return;
            }

            const data = await response.json();

            if (!data || data.length === 0) {
                tableBody.innerHTML = '<tr><td colspan="6">No branches found.</td></tr>';
                return;
            }

            tableBody.innerHTML = '';

            data.forEach(b => {
                const tr = document.createElement('tr');
                tr.setAttribute('data-id', b.branchId);

                tr.innerHTML = `
                    <td>${b.branchId}</td>
                    <td>${b.name ?? ''}</td>
                    <td>${b.location ?? ''}</td>
                    <td>${b.state ?? ''}</td>
                    <td>${mapStatusText(b.status)}</td>
                    <td>
                    <button type="button"
                class="btn btn-sm btn-secondary btn-branch-edit"
                data-id="${b.branchId}"
                title="Edit">
            <i class="fas fa-edit"></i>
        </button>
        <button type="button"
                class="btn btn-sm btn-danger btn-branch-delete ms-1"
                data-id="${b.branchId}"
                title="Delete">
            <i class="fas fa-trash"></i>
        </button>                    </td>
                `;

                tableBody.appendChild(tr);
            });
        } catch (err) {
            console.error(err);
            tableBody.innerHTML = '<tr><td colspan="6">Error loading branches.</td></tr>';
        }
    }

    async function loadStates() {
        if (!txtState) return;

        // Clear existing options
        txtState.innerHTML = '';

        // Add default option
        const defaultOpt = document.createElement('option');
        defaultOpt.value = '';
        defaultOpt.textContent = '-- Select State --';
        txtState.appendChild(defaultOpt);

        try {
            const response = await fetch('/Branch/GetStates');

            if (!response.ok) {
                console.error('Failed to load states');
                return;
            }

            const states = await response.json(); // [{stateId, stateName}, ...]

            states.forEach(s => {
                const opt = document.createElement('option');
                opt.value = s.stateName;      // we store stateName in Branch_State column
                opt.textContent = s.stateName;
                txtState.appendChild(opt);
            });
        } catch (err) {
            console.error('Error loading states', err);
        }
    }

    async function openEditBranchModal(branchId) {
        clearForm();
        if (hiddenIsNew) hiddenIsNew.value = 'false';
        const label = document.getElementById('branchModalLabel');
        if (label) label.textContent = 'Edit Branch';

        try {
            const response = await fetch('/Branch/GetBranch?branchId=' + encodeURIComponent(branchId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                if (msg) msg.textContent = 'Error loading branch details.';
                if (modal) modal.show();
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (msg) msg.textContent = result.message || 'Branch not found.';
                if (modal) modal.show();
                return;
            }

            const b = result.data;

            txtId.value = b.branchId || '';
            txtName.value = b.name || '';
            txtLocation.value = b.location || '';
            txtState.value = b.state || '';
            selStatus.value = b.status ? '1' : '0';

            if (modal) modal.show();
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'Error loading branch details.';
            if (modal) modal.show();
        }
    }

    async function saveBranch() {
        if (!txtId) return;

        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-success');
            msg.classList.add('text-danger');
        }

        const isNew = hiddenIsNew && hiddenIsNew.value === 'true';

        const payload = {
            isNew: isNew,
            branchId: txtId.value.trim(),
            name: txtName.value.trim(),
            location: txtLocation.value.trim(),
            state: txtState.value.trim(),
            status: selStatus ? (selStatus.value === '1') : true
        };

        if (!payload.branchId || !payload.name || !payload.location || !payload.state) {
            if (msg) msg.textContent = 'Please fill in all required fields.';
            return;
        }

        try {
            const response = await fetch('/Branch/SaveBranch', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                if (msg) msg.textContent = 'Server error while saving branch.';
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (msg) msg.textContent = result.message || 'Failed to save branch.';
                return;
            }

            if (msg) msg.textContent = '';
            if (modal) modal.hide();

            loadStates();
            loadBranches();
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'An unexpected error occurred.';
        }
    }

    async function deleteBranch(branchId) {
        if (!confirm('Are you sure you want to delete this branch?')) {
            return;
        }

        try {
            const response = await fetch('/Branch/DeleteBranch', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ branchId: branchId })
            });

            if (!response.ok) {
                alert('Server error while deleting branch.');
                return;
            }

            const result = await response.json();

            if (!result.success) {
                alert(result.message || 'Failed to delete branch.');
                return;
            }

            loadStates();
            loadBranches();
        } catch (err) {
            console.error(err);
            alert('An unexpected error occurred.');
        }
    }

    function attachRowHandlers() {
        document.addEventListener('click', function (e) {
            const target = e.target;

            const editBtn = target.closest('.btn-branch-edit');
            if (editBtn) {
                const id = editBtn.getAttribute('data-id');
                if (id) {
                    openEditBranchModal(id);
                }
            }

            const deleteBtn = target.closest('.btn-branch-delete');
            if (deleteBtn) {
                const id = deleteBtn.getAttribute('data-id');
                if (id) {
                    deleteBranch(id);
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        loadStates();
        loadBranches();
        attachRowHandlers();

        if (btnAdd) {
            btnAdd.addEventListener('click', function () {
                clearForm();
                if (modal) modal.show();
            });
        }

        if (btnSave) {
            // we'll get btnSave via ID
        }
    });

    const btnSave = document.getElementById('btnSaveBranch');
    if (btnSave) {
        btnSave.addEventListener('click', saveBranch);
    }
})();