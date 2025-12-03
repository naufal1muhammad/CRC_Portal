// @ts-nocheck
(function() {
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
    const selType = document.getElementById('StaffType');
    const fileInput = document.getElementById('StaffFiles');
    const hiddenIsNew = document.getElementById('StaffIsNew');
    const msg = document.getElementById('staffFormMessage');
    const btnSave = document.getElementById('btnSaveStaff');
    const mandatoryContainer = document.getElementById('mandatoryDocumentsContainer');

    const tableBody = document.querySelector('#staffTable tbody');
    const pendingDocDeletes = new Set();

    // -------------------------
    // Helpers
    // -------------------------

    function clearForm() {
        if (!txtId) return;

        txtId.value = '';
        txtName.value = '';
        txtNRIC.value = '';
        txtPhone.value = '';
        txtEmail.value = '';

        if (selBranch) selBranch.value = '';
        if (selType) {
            selType.value = '';
            selType.disabled = false; // editable for NEW
        }

        if (hiddenIsNew) hiddenIsNew.value = 'true';
        pendingDocDeletes.clear();

        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-success');
            msg.classList.add('text-danger');
        }

        if (fileInput) fileInput.value = '';

        if (mandatoryContainer) {
            mandatoryContainer.innerHTML = '<p class="text-muted mb-0">Select a staff type to see required documents.</p>';
        }

        const label = document.getElementById('staffModalLabel');
        if (label) label.textContent = 'Add Staff';
    }

    async function loadStaffDocumentsRaw(staffId) {
        if (!staffId) return [];

        try {
            const response = await fetch('/Staff/GetStaffDocuments?staffId=' + encodeURIComponent(staffId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            const result = await response.json();

            if (!response.ok || !result.success) {
                console.error('Error loading staff documents', result.message);
                return [];
            }

            return result.data || [];
        } catch (err) {
            console.error('Error loading staff documents', err);
            return [];
        }
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

    async function loadStaffTypes() {
        if (!selType) return;

        selType.innerHTML = '<option value="">Loading staff types...</option>';

        try {
            const response = await fetch('/Staff/GetStaffTypes', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                selType.innerHTML = '<option value="">Error loading staff types</option>';
                return;
            }

            const data = await response.json();

            if (!data || data.length === 0) {
                selType.innerHTML = '<option value="">No staff types found</option>';
                return;
            }

            selType.innerHTML = '<option value="">-- Select Staff Type --</option>';

            data.forEach(t => {
                const opt = document.createElement('option');
                opt.value = t.staffTypeId;
                opt.textContent = t.staffTypeName;
                selType.appendChild(opt);
            });
        } catch (err) {
            console.error('Error loading staff types', err);
            selType.innerHTML = '<option value="">Error loading staff types</option>';
        }
    }

    async function renderMandatoryDocuments(staffTypeId, existingDocs) {
        if (!mandatoryContainer) return;

        if (!staffTypeId) {
            mandatoryContainer.innerHTML = '<p class="text-muted mb-0">Select a staff type to see required documents.</p>';
            return;
        }

        mandatoryContainer.innerHTML = '<p class="text-muted mb-0">Loading mandatory documents...</p>';

        try {
            const response = await fetch('/Staff/GetMandatoryDocumentsForStaffType?staffTypeId=' + encodeURIComponent(staffTypeId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            const result = await response.json();

            if (!response.ok || !result.success) {
                mandatoryContainer.innerHTML = '<p class="text-danger mb-0">Error loading mandatory documents.</p>';
                return;
            }

            const docs = result.data || [];

            if (docs.length === 0) {
                mandatoryContainer.innerHTML = '<p class="text-muted mb-0">No mandatory documents configured for this staff type.</p>';
                return;
            }

            const wrapper = document.createElement('div');

            docs.forEach(d => {
                const card = document.createElement('div');
                card.className = 'card mb-2 mandatory-doc-card';
                card.setAttribute('data-doc-type-id', d.staffDocumentTypeId);
                card.setAttribute('data-doc-type-name', d.staffDocumentTypeName);

                const body = document.createElement('div');
                body.className = 'card-body';

                body.innerHTML = `
                    <label class="form-label fw-semibold">
                        ${d.staffDocumentTypeName} <span class="text-danger">*</span>
                    </label>
                    <input type="file" class="form-control mandatory-doc-file" multiple />
                    <div class="form-text">
                        Upload one or more files for this document type.
                    </div>
                `;

                // Existing documents for this type (if any)
                const docsForType = (existingDocs || []).filter(x =>
                    (x.staffDocumentTypeId || '').toString().toLowerCase() ===
                    (d.staffDocumentTypeId || '').toString().toLowerCase()
                );

                if (docsForType.length > 0) {
                    const listWrapper = document.createElement('div');
                    listWrapper.className = 'mt-2';

                    const title = document.createElement('div');
                    title.className = 'small text-muted mb-1';
                    title.textContent = 'Existing documents for this type:';
                    listWrapper.appendChild(title);

                    const ul = document.createElement('ul');
                    ul.className = 'list-group';

                    docsForType.forEach(doc => {
                        const li = document.createElement('li');
                        li.className = 'list-group-item d-flex justify-content-between align-items-center existing-doc-item';
                        li.setAttribute('data-doc-id', doc.documentId);

                        const left = document.createElement('div');
                        left.innerHTML = `
                            <a href="${doc.filePath}" target="_blank">${doc.fileName}</a>
                            <br />
                            <small class="text-muted">${doc.uploadedOn || ''}</small>
                        `;

                        const right = document.createElement('div');
                        right.innerHTML = `
                            <button type="button"
                                    class="btn btn-sm btn-outline-danger btn-staff-doc-delete"
                                    data-id="${doc.documentId}">
                                Delete
                            </button>
                        `;

                        li.appendChild(left);
                        li.appendChild(right);
                        ul.appendChild(li);
                    });

                    listWrapper.appendChild(ul);
                    body.appendChild(listWrapper);
                }

                card.appendChild(body);
                wrapper.appendChild(card);
            });

            mandatoryContainer.innerHTML = '';
            mandatoryContainer.appendChild(wrapper);
        } catch (err) {
            console.error('Error loading mandatory documents', err);
            mandatoryContainer.innerHTML = '<p class="text-danger mb-0">Error loading mandatory documents.</p>';
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
                    <td>${s.staffTypeId ?? ''}</td>
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
                        </button>
                    </td>
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
                return;
            }

            const idText = (cells[0].textContent || '').toLowerCase();
            const nameText = (cells[1].textContent || '').toLowerCase();
            const combined = idText + ' ' + nameText;

            row.style.display = !filter || combined.includes(filter) ? '' : 'none';
        });
    }

    async function uploadStaffDocuments(staffId, staffName) {
        const formData = new FormData();
        let hasFile = false;

        // Mandatory docs
        if (mandatoryContainer) {
            const cards = mandatoryContainer.querySelectorAll('.mandatory-doc-card');
            cards.forEach(card => {
                const docTypeId = card.getAttribute('data-doc-type-id') || '';
                const docTypeName = card.getAttribute('data-doc-type-name') || '';
                const input = card.querySelector('.mandatory-doc-file');

                if (input && input.files && input.files.length > 0) {
                    for (let i = 0; i < input.files.length; i++) {
                        const file = input.files[i];
                        formData.append('files', file);
                        formData.append('docTypeIds', docTypeId);
                        formData.append('docTypeNames', docTypeName);
                        hasFile = true;
                    }
                }
            });
        }

        // Optional generic extra files
        if (fileInput && fileInput.files && fileInput.files.length > 0) {
            for (let i = 0; i < fileInput.files.length; i++) {
                const file = fileInput.files[i];
                formData.append('files', file);
                formData.append('docTypeIds', '');
                formData.append('docTypeNames', '');
                hasFile = true;
            }
        }

        if (!hasFile) {
            return; // nothing to upload
        }

        formData.append('staffId', staffId);
        formData.append('staffName', staffName || '');

        try {
            const response = await fetch('/Staff/UploadStaffDocuments', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                alert('Error uploading staff documents.');
                return;
            }

            const result = await response.json();

            if (!result.success) {
                alert(result.message || 'Failed to upload staff documents.');
                return;
            }

        } catch (err) {
            console.error(err);
            alert('An unexpected error occurred while uploading staff documents.');
        }
    }

    function openAddStaffModal() {
        clearForm();

        const tasks = [];

        if (selBranch && typeof loadBranches === 'function') {
            tasks.push(loadBranches());
        }
        if (selType) {
            tasks.push(loadStaffTypes());
        }

        Promise.all(tasks).finally(() => {
            if (modal) modal.show();
        });
    }

    async function openEditStaffModal(staffId) {
        clearForm();
        if (hiddenIsNew) hiddenIsNew.value = 'false';

        const label = document.getElementById('staffModalLabel');
        if (label) label.textContent = 'Edit Staff';

        try {
            const tasks = [];

            if (selBranch && typeof loadBranches === 'function') {
                tasks.push(loadBranches());
            }

            if (selType) {
                tasks.push(loadStaffTypes());
            }

            await Promise.all(tasks);

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

            txtId.value = s.staffId || '';
            txtName.value = s.name || '';
            txtNRIC.value = s.nric || '';
            txtPhone.value = s.phone || '';
            txtEmail.value = s.email || '';

            if (selBranch && s.branchId) {
                selBranch.value = s.branchId;
            }

            if (selType && s.staffTypeId) {
                selType.value = s.staffTypeId;
                selType.disabled = true; // cannot change staff type in edit
            }

            let existingDocs = [];
            if (txtId.value) {
                existingDocs = await loadStaffDocumentsRaw(txtId.value);
            }

            if (s.staffTypeId) {
                await renderMandatoryDocuments(s.staffTypeId, existingDocs);
            } else {
                await renderMandatoryDocuments('', null);
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

        const staffTypeId = selType ? selType.value : '';

        const payload = {
            isNew: isNew,
            staffId: txtId.value.trim(),
            name: txtName.value.trim(),
            nric: txtNRIC.value.trim(),
            phone: txtPhone.value.trim(),
            email: txtEmail.value.trim(),
            branchId: branchId,
            branchName: branchName,
            staffTypeId: staffTypeId
        };

        if (!payload.name || !payload.nric || !payload.phone || !payload.email ||
            !payload.branchId || !payload.staffTypeId) {
            if (msg) msg.textContent = 'Please fill in all mandatory fields.';
            return;
        }

        if (!isNew && !payload.staffId) {
            if (msg) msg.textContent = 'Staff ID is missing for update.';
            return;
        }

        // Validate mandatory documents for BOTH new & edit
        if (mandatoryContainer) {
            const cards = mandatoryContainer.querySelectorAll('.mandatory-doc-card');
            for (const card of cards) {
                const input = card.querySelector('.mandatory-doc-file');
                const docName = card.getAttribute('data-doc-type-name') || 'Document';

                const existingItems = card.querySelectorAll('.existing-doc-item');
                const existingCount = existingItems.length;

                const newFilesCount = (input && input.files) ? input.files.length : 0;

                if (existingCount === 0 && newFilesCount === 0) {
                    if (msg) {
                        msg.textContent = `Please upload file(s) for: ${docName}.`;
                    }
                    return;
                }
            }
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

            let staffId = payload.staffId;
            if (isNew && result.staffId) {
                staffId = result.staffId; // auto-generated ID from SP
            }

            if (staffId) {
                await uploadStaffDocuments(staffId, payload.name);
                await applyPendingDocumentDeletes(staffId);
            }

            if (msg) msg.textContent = '';
            if (modal) modal.hide();

            if (typeof loadStaffList === 'function') {
                loadStaffList();
            }
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

function deleteStaffDocument(documentId, clickedButton) {
    if (!confirm('Are you sure you want to delete this document?')) {
        return;
    }

    pendingDocDeletes.add(documentId);

    if (clickedButton) {
        const li = clickedButton.closest('.existing-doc-item') 
                || clickedButton.closest('li');
        if (li) li.remove();
    }
}

async function applyPendingDocumentDeletes(staffId) {
    if (!staffId) return;
    if (pendingDocDeletes.size === 0) return;

    try {
        for (const documentId of pendingDocDeletes) {
            const response = await fetch('/Staff/DeleteStaffDocument', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ documentId })
            });
        }
        pendingDocDeletes.clear();
    } catch (err) {
        console.error('Error applying pending document deletes', err);
    }
}


    function attachRowActionHandlers() {
        document.addEventListener('click', function(e) {
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
                    deleteStaffDocument(docId, docDeleteBtn);
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function() {
        if (typeof loadStaffList === 'function') {
            loadStaffList();
        }

        if (typeof loadBranches === 'function') {
            loadBranches();
        }
        loadStaffTypes();

        if (btnAdd) {
            btnAdd.addEventListener('click', openAddStaffModal);
        }

        if (btnSave) {
            btnSave.addEventListener('click', saveStaff);
        }

        if (selType) {
            selType.addEventListener('change', function() {
                const staffTypeId = selType.value;
                if (hiddenIsNew && hiddenIsNew.value === 'true') {
                    renderMandatoryDocuments(staffTypeId, null);
                }
            });
        }

        if (typeof attachRowActionHandlers === 'function') {
            attachRowActionHandlers();
        }

        if (txtSearch) {
            txtSearch.addEventListener('input', function() {
                applyStaffSearchFilter();
            });
        }
    });
})();