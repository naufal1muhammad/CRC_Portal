// @ts-nocheck
(function () {
    const btnAdd = document.getElementById('btnAddStaff');
    const txtSearch = document.getElementById('staffSearch');
    const modalEl = document.getElementById('staffModal');
    const modal = modalEl ? new bootstrap.Modal(modalEl) : null;

    const txtId = document.getElementById('StaffId');
    const txtName = document.getElementById('StaffName');
    const txtNRIC = document.getElementById('StaffNRIC');
    const txtPhone = document.getElementById('StaffPhone');
    const txtEmail = document.getElementById('StaffEmail');
    const selBranch = document.getElementById('StaffBranch');
    const selStaffType = document.getElementById('StaffType');
    const fileInput = document.getElementById('StaffFiles');
    const docsList = document.getElementById('staffDocumentsList');
    const hiddenIsNew = document.getElementById('StaffIsNew');
    const msg = document.getElementById('staffFormMessage');
    const btnSave = document.getElementById('btnSaveStaff');

    const tableBody = document.querySelector('#staffTable tbody');

    function mapStaffTypeName(typeId) {
        const t = parseInt(typeId, 10);
        switch (t) {
            case 1: return 'Doctor';
            case 2: return 'Nurse';
            case 3: return 'Counsellor';
            case 4: return 'Clerk';
            default: return '';
        }
    }

    function clearDocumentsUI() {
        if (fileInput) {
            fileInput.value = '';
        }
        if (docsList) {
            docsList.innerHTML = '<p class="text-muted mb-0">No documents loaded.</p>';
        }
    }

    function clearForm() {
        if (!txtId) return;

        txtId.value = '';
        txtName.value = '';
        txtNRIC.value = '';
        txtPhone.value = '';
        txtEmail.value = '';
        if (selBranch) selBranch.value = '';
        if (selStaffType) selStaffType.value = '';
        if (hiddenIsNew) hiddenIsNew.value = 'true';
        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-success');
            msg.classList.add('text-danger');
        }
        const label = document.getElementById('staffModalLabel');
        if (label) label.textContent = 'Add Staff';

        clearDocumentsUI();
    }

    async function loadBranches() {
        if (!selBranch) return;

        selBranch.innerHTML = '<option value="">Loading...</option>';

        try {
            const response = await fetch('/Staff/GetActiveBranches', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                selBranch.innerHTML = '<option value="">Error loading branches</option>';
                return;
            }

            const data = await response.json();

            selBranch.innerHTML = '<option value="">-- Select Branch --</option>';

            data.forEach(b => {
                const opt = document.createElement('option');
                opt.value = b.branchId;
                opt.textContent = b.branchName;
                selBranch.appendChild(opt);
            });
        } catch (err) {
            console.error(err);
            selBranch.innerHTML = '<option value="">Error loading branches</option>';
        }
    }

    async function loadStaffList() {
        if (!tableBody) return;

        tableBody.innerHTML = '<tr><td colspan="8">Loading...</td></tr>';

        try {
            const response = await fetch('/Staff/GetStaffList', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                tableBody.innerHTML = '<tr><td colspan="8">Error loading staff.</td></tr>';
                return;
            }

            const data = await response.json();

            if (!data || data.length === 0) {
                tableBody.innerHTML = '<tr><td colspan="8">No staff found.</td></tr>';
                return;
            }

            tableBody.innerHTML = '';

            data.forEach(s => {
                const tr = document.createElement('tr');
                tr.setAttribute('data-id', s.staffId);

                tr.innerHTML = `
                    <td>${s.staffId}</td>
                    <td>${s.name ?? ''}</td>
                    <td>${s.nric ?? ''}</td>
                    <td>${s.phone ?? ''}</td>
                    <td>${s.email ?? ''}</td>
                    <td>${s.branchName ?? ''}</td>
                    <td>${mapStaffTypeName(s.staffType)}</td>
                    <td>
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
        </button>                    </td>
                `;

                tableBody.appendChild(tr);
            });
            applyStaffSearchFilter();
        } catch (err) {
            console.error(err);
            tableBody.innerHTML = '<tr><td colspan="8">Error loading staff.</td></tr>';
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
            return; // skip if row not data row
        }

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


    async function loadStaffDocuments(staffId) {
        if (!docsList) return;

        console.log('Loading staff documents for:', staffId);

        if (!staffId) {
            clearDocumentsUI();
            return;
        }

        docsList.innerHTML = '<p class="text-muted mb-0">Loading documents...</p>';

        try {
            const response = await fetch('/Staff/GetStaffDocuments?staffId=' + encodeURIComponent(staffId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                docsList.innerHTML = '<p class="text-danger mb-0">Error loading documents.</p>';
                return;
            }

            const result = await response.json();
            console.log('Staff documents result:', result);

            if (!result.success || !result.data || result.data.length === 0) {
                docsList.innerHTML = '<p class="text-muted mb-0">No documents uploaded.</p>';
                return;
            }

            const list = result.data;

            const ul = document.createElement('ul');
            ul.className = 'list-group';

            list.forEach(d => {
                const li = document.createElement('li');
                li.className = 'list-group-item d-flex justify-content-between align-items-center';
                li.setAttribute('data-doc-id', d.documentId);

                const left = document.createElement('div');
                left.innerHTML = `<a href="${d.filePath}" target="_blank">${d.fileName}</a><br /><small class="text-muted">${d.uploadedOn ?? ''}</small>`;

                const right = document.createElement('div');
                right.innerHTML = `
                    <button type="button" class="btn btn-sm btn-outline-danger btn-staff-doc-delete" data-id="${d.documentId}">
                        Delete
                    </button>
                `;

                li.appendChild(left);
                li.appendChild(right);
                ul.appendChild(li);
            });

            docsList.innerHTML = '';
            docsList.appendChild(ul);

        } catch (err) {
            console.error(err);
            docsList.innerHTML = '<p class="text-danger mb-0">Error loading documents.</p>';
        }
    }

    async function uploadStaffFiles(staffId) {
        if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
            return;
        }

        const formData = new FormData();
        formData.append('staffId', staffId);
        formData.append('staffName',txtName.value.trim());

        for (let i = 0; i < fileInput.files.length; i++) {
            const file = fileInput.files[i];
            formData.append('files', file);
        }

        try {
            const response = await fetch('/Staff/UploadStaffDocuments', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                alert('Error uploading staff files.');
                return;
            }

            const result = await response.json();

            if (!result.success) {
                alert(result.message || 'Failed to upload staff files.');
                return;
            }

            fileInput.value = '';

            await loadStaffDocuments(staffId);
        } catch (err) {
            console.error(err);
            alert('An unexpected error occurred while uploading staff files.');
        }
    }

    function openAddStaffModal() {
        clearForm();
        if (hiddenIsNew) hiddenIsNew.value = 'true';
        if (modal) modal.show();
    }

    async function openEditStaffModal(staffId) {
        clearForm();
        if (hiddenIsNew) hiddenIsNew.value = 'false';
        const label = document.getElementById('staffModalLabel');
        if (label) label.textContent = 'Edit Staff';

        try {
            const response = await fetch('/Staff/GetStaff?staffId=' + encodeURIComponent(staffId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                if (msg) msg.textContent = 'Error loading staff details.';
                if (modal) modal.show();
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (msg) msg.textContent = result.message || 'Staff not found.';
                if (modal) modal.show();
                return;
            }

            const s = result.data;

            await loadBranches();

            txtId.value = s.staffId || '';
            txtName.value = s.name || '';
            txtNRIC.value = s.nric || '';
            txtPhone.value = s.phone || '';
            txtEmail.value = s.email || '';

            if (s.branchId) {
                selBranch.value = s.branchId;
            }

            selStaffType.value = s.staffType ? String(s.staffType) : '';

            if (txtId.value) {
                await loadStaffDocuments(txtId.value);
            } else {
                clearDocumentsUI();
            }

            if (modal) modal.show();
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'Error loading staff details.';
            if (modal) modal.show();
        }
    }

    async function saveStaff() {
        if (!txtId) return;

        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-success');
            msg.classList.add('text-danger');
        }

        const isNew = hiddenIsNew && hiddenIsNew.value === 'true';

        const branchId = selBranch ? selBranch.value : '';
        const branchName = selBranch && selBranch.selectedIndex >= 0
            ? selBranch.options[selBranch.selectedIndex].text
            : '';

        const staffTypeVal = selStaffType ? parseInt(selStaffType.value || '0', 10) : 0;

        const payload = {
            isNew: isNew,
            staffId: txtId.value.trim(),
            name: txtName.value.trim(),
            nric: txtNRIC.value.trim(),
            phone: txtPhone.value.trim(),
            email: txtEmail.value.trim(),
            branchId: branchId,
            branchName: branchName,
            staffType: staffTypeVal
        };

        if (!payload.staffId || !payload.name || !payload.nric || !payload.phone ||
            !payload.email || !payload.branchId || payload.staffType <= 0) {
            if (msg) msg.textContent = 'Please fill in all required fields.';
            return;
        }

        try {
            const response = await fetch('/Staff/SaveStaff', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                if (msg) msg.textContent = 'Server error while saving staff.';
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (msg) msg.textContent = result.message || 'Failed to save staff.';
                return;
            }

            // upload files if any
            const staffId = payload.staffId;

            if (fileInput && fileInput.files && fileInput.files.length > 0) {
                await uploadStaffFiles(staffId);
            }

            if (msg) msg.textContent = '';
            if (modal) modal.hide();

            loadStaffList();
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'An unexpected error occurred.';
        }
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
                    'Accept': 'application/json'
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

    async function deleteStaffDocument(documentId) {
        if (!confirm('Are you sure you want to delete this document?')) {
            return;
        }

        try {
            const response = await fetch('/Staff/DeleteStaffDocument', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ documentId: documentId })
            });

            if (!response.ok) {
                alert('Server error while deleting document.');
                return;
            }

            const result = await response.json();

            if (!result.success) {
                alert(result.message || 'Failed to delete document.');
                return;
            }

            const staffId = txtId ? txtId.value.trim() : null;
            if (staffId) {
                loadStaffDocuments(staffId);
            }
        } catch (err) {
            console.error(err);
            alert('An unexpected error occurred while deleting document.');
        }
    }

    function attachRowActionHandlers() {
        document.addEventListener('click', function (e) {
            const target = e.target;

            const editBtn = target.closest('.btn-staff-edit');
            if (editBtn) {
                const id = editBtn.getAttribute('data-id');
                if (id) {
                    openEditStaffModal(id);
                }
            }

            const deleteBtn = target.closest('.btn-staff-delete');
            if (deleteBtn) {
                const id = deleteBtn.getAttribute('data-id');
                if (id) {
                    deleteStaff(id);
                }
            }

            const docDeleteBtn = target.closest('.btn-staff-doc-delete');
            if (docDeleteBtn) {
                const idStr = docDeleteBtn.getAttribute('data-id');
                const docId = idStr ? parseInt(idStr, 10) : 0;
                if (docId > 0) {
                    deleteStaffDocument(docId);
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        loadBranches();
        loadStaffList();
        attachRowActionHandlers();

        if (btnAdd) {
            btnAdd.addEventListener('click', function () {
                loadBranches().then(() => {
                    openAddStaffModal();
                });
            });
        }

        if (btnSave) {
            btnSave.addEventListener('click', saveStaff);
        }

        if (txtSearch) {
        txtSearch.addEventListener('input', function () {
            applyStaffSearchFilter();
            });
        }
    });
})();