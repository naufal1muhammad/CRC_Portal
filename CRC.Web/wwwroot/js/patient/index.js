// @ts-nocheck
(function() {
    const stages = ['T2', 'T3', 'T4', 'T5'];

    const btnAdd = document.getElementById('btnAddPatient');
    const txtSearch = document.getElementById('patientSearch');
    const modalEl = document.getElementById('patientModal');
    const modal = modalEl ? new bootstrap.Modal(modalEl) : null;

    const txtId = document.getElementById('PatientId');
    const txtName = document.getElementById('PatientName');
    const txtNRIC = document.getElementById('PatientNRIC');
    const txtPhone = document.getElementById('PatientPhone');
    const txtEmail = document.getElementById('PatientEmail');
    const selBranch = document.getElementById('PatientBranch');
    const selStage = document.getElementById('PatientStage');
    const txtAppointmentDate = document.getElementById('PatientAppointmentDate');
    const txtRemarks = document.getElementById('PatientRemarks');
    const hiddenIsNew = document.getElementById('PatientIsNew');
    const msg = document.getElementById('patientFormMessage');
    const fileInput = document.getElementById('PatientFiles');
    const docsList = document.getElementById('patientDocumentsList');
    const btnSave = document.getElementById('btnSavePatient');

    function getCurrentStage() {
        const activeTab = document.querySelector('#patientStageTabs .nav-link.active');
        return activeTab ? activeTab.getAttribute('data-stage') : 'T2';
    }

    async function loadBranches() {
        if (!selBranch) return;

        selBranch.innerHTML = '<option value="">Loading...</option>';

        try {
            const response = await fetch('/Patient/GetActiveBranches', {
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

                opt.setAttribute('data-branch-name', b.branchName || '');
                opt.setAttribute('data-branch-state', b.state || b.branchState || '');

                selBranch.appendChild(opt);
            });
        } catch (err) {
            console.error(err);
            selBranch.innerHTML = '<option value="">Error loading branches</option>';
        }
    }

    async function loadPatients(stage) {
        const table = document.querySelector('#table-' + stage + ' tbody');
        if (!table) return;

        table.innerHTML = '<tr><td colspan="9">Loading...</td></tr>';

        try {
            const response = await fetch('/Patient/GetPatients?stage=' + encodeURIComponent(stage), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                table.innerHTML = '<tr><td colspan="9">Error loading patients.</td></tr>';
                return;
            }

            const data = await response.json();

            if (!data || data.length === 0) {
                table.innerHTML = '<tr><td colspan="9">No patients found.</td></tr>';
                return;
            }

            table.innerHTML = '';

            data.forEach(p => {
                const tr = document.createElement('tr');
                tr.setAttribute('data-id', p.patientId);

                tr.innerHTML = `
                    <td>${p.patientId}</td>
                    <td>${p.name ?? ''}</td>
                    <td>${p.nric ?? ''}</td>
                    <td>${p.phone ?? ''}</td>
                    <td>${p.email ?? ''}</td>
                    <td>${p.branchName ?? ''}</td>
                    <td>${p.stage ?? ''}</td>
                    <td>${p.appointmentDate ?? ''}</td>
                    <td>
                    <button type="button"
                class="btn btn-sm btn-secondary btn-patient-edit"
                data-id="${p.patientId}"
                title="Edit">
            <i class="fas fa-edit"></i>
        </button>
        <button type="button"
                class="btn btn-sm btn-danger btn-patient-delete ms-1"
                data-id="${p.patientId}"
                title="Delete">
            <i class="fas fa-trash"></i>
        </button>
                    </td>`;

                table.appendChild(tr);
            });
            applyPatientSearchFilter(stage);
        } catch (err) {
            console.error(err);
            table.innerHTML = '<tr><td colspan="9">Error loading patients.</td></tr>';
        }
    }

    function applyPatientSearchFilter(stage) {
    if (!txtSearch) return;

    const filter = txtSearch.value.trim().toLowerCase();
    const targetStage = stage || getCurrentStage();

    const tbody = document.querySelector('#table-' + targetStage + ' tbody');
    if (!tbody) return;

    const rows = tbody.querySelectorAll('tr');

    rows.forEach(row => {
        const cells = row.querySelectorAll('td');
        if (cells.length < 2) {
            return; // skip weird rows
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

    async function loadPatientDocuments(patientId) {
        if (!docsList) return;

        if (!patientId) {
            clearDocumentsUI();
            return;
        }

        docsList.innerHTML = '<p class="text-muted mb-0">Loading documents...</p>';

        try {
            const response = await fetch('/Patient/GetPatientDocuments?patientId=' + encodeURIComponent(patientId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                docsList.innerHTML = '<p class="text-danger mb-0">Error loading documents.</p>';
                return;
            }

            const result = await response.json();

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
                    <button type="button" class="btn btn-sm btn-outline-danger btn-doc-delete" data-id="${d.documentId}">
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

    async function uploadPatientFiles(patientId) {
        if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
            return; // nothing to upload
        }

        const formData = new FormData();
        formData.append('patientId', patientId);
        formData.append('patientName', txtName.value.trim());

        for (let i = 0; i < fileInput.files.length; i++) {
            const file = fileInput.files[i];
            formData.append('files', file);
        }

        try {
            const response = await fetch('/Patient/UploadPatientDocuments', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                alert('Error uploading files.');
                return;
            }

            const result = await response.json();

            if (!result.success) {
                alert(result.message || 'Failed to upload files.');
                return;
            }

            // Clear file input
            fileInput.value = '';

            // Reload documents list
            await loadPatientDocuments(patientId);
        } catch (err) {
            console.error(err);
            alert('An unexpected error occurred while uploading files.');
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
        selBranch.value = '';
        selStage.value = 'T2';
        txtAppointmentDate.value = '';
        txtRemarks.value = '';
        hiddenIsNew.value = 'true';
        msg.textContent = '';
        msg.classList.remove('text-success');
        msg.classList.add('text-danger');
        const label = document.getElementById('patientModalLabel');
        if (label) label.textContent = 'Add Patient';

        clearDocumentsUI();
    }

    function openAddPatientModal() {
        clearForm();
        const currentStage = getCurrentStage();
        selStage.value = currentStage;
        hiddenIsNew.value = 'true';
        clearDocumentsUI();
        if (modal) modal.show();
    }

    async function openEditPatientModal(patientId) {
        clearForm();
        hiddenIsNew.value = 'false';
        const label = document.getElementById('patientModalLabel');
        if (label) label.textContent = 'Edit Patient';

        try {
            const response = await fetch('/Patient/GetPatient?patientId=' + encodeURIComponent(patientId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                msg.textContent = 'Error loading patient details.';
                if (modal) modal.show();
                return;
            }

            const result = await response.json();

            if (!result.success) {
                msg.textContent = result.message || 'Patient not found.';
                if (modal) modal.show();
                return;
            }

            const p = result.data;

            // load branches first so the dropdown has options
            await loadBranches();

            // fill form
            txtId.value = p.patientId || '';
            txtName.value = p.name || '';
            txtNRIC.value = p.nric || '';
            txtPhone.value = p.phone || '';
            txtEmail.value = p.email || '';

            if (p.branchId) {
                selBranch.value = p.branchId;
            }

            selStage.value = p.stage || 'T2';
            txtAppointmentDate.value = p.appointmentDate || '';
            txtRemarks.value = p.remarks || '';

            // IMPORTANT: load documents using the current ID in the textbox
            if (txtId.value) {
                await loadPatientDocuments(txtId.value);
            } else {
                clearDocumentsUI();
            }

            if (modal) modal.show();
        } catch (err) {
            console.error(err);
            msg.textContent = 'Error loading patient details.';
            if (modal) modal.show();
        }
    }

async function savePatient() {
    if (!txtId) return;

    msg.textContent = '';
    msg.classList.remove('text-success');
    msg.classList.add('text-danger');

    const isNew = hiddenIsNew.value === 'true';

    // ✅ Get branchId, branchName, branchState from selected option
    let branchId = '';
    let branchName = '';
    let branchState = '';

    if (selBranch && selBranch.value) {
        const selectedOption = selBranch.options[selBranch.selectedIndex];

        branchId = selBranch.value;
        branchName =
            selectedOption.getAttribute('data-branch-name')
            || selectedOption.textContent
            || '';

        branchState = selectedOption.getAttribute('data-branch-state') || '';
    }

    const payload = {
        isNew: isNew,
        patientId: txtId.value.trim(),
        name: txtName.value.trim(),
        nric: txtNRIC.value.trim(),
        phone: txtPhone.value.trim(),
        email: txtEmail.value.trim(),
        branchId: branchId,
        branchName: branchName,
        branchState: branchState,
        stage: selStage.value,
        remarks: txtRemarks.value.trim(),
        appointmentDate: txtAppointmentDate.value || null
    };

    if (!payload.patientId || !payload.name || !payload.nric || !payload.phone || !payload.email ||
        !payload.branchId || !payload.stage) {
        msg.textContent = 'Please fill in all required fields.';
        return;
    }

    try {
        console.log('savePatient payload:', payload);
        const response = await fetch('/Patient/SavePatient', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            msg.textContent = 'Server error while saving patient.';
            return;
        }

        const result = await response.json();

        if (!result.success) {
            msg.textContent = result.message || 'Failed to save patient.';
            return;
        }

        // Patient saved successfully; now handle file uploads (if any)
        const patientId = payload.patientId;

        if (fileInput && fileInput.files && fileInput.files.length > 0) {
            await uploadPatientFiles(patientId);
        }

        msg.textContent = '';
        if (modal) modal.hide();

        // Reload all stages to reflect stage changes
        stages.forEach(s => loadPatients(s));
    } catch (err) {
        console.error(err);
        msg.textContent = 'An unexpected error occurred.';
    }
}

    async function deletePatient(patientId) {
        if (!confirm('Are you sure you want to delete this patient?')) {
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

            if (!response.ok) {
                alert('Server error while deleting patient.');
                return;
            }

            const result = await response.json();

            if (!result.success) {
                alert(result.message || 'Failed to delete patient.');
                return;
            }

            // Reload all stages
            stages.forEach(s => loadPatients(s));
        } catch (err) {
            console.error(err);
            alert('An unexpected error occurred.');
        }
    }

    async function deletePatientDocument(documentId) {
        if (!confirm('Are you sure you want to delete this document?')) {
            return;
        }

        try {
            const response = await fetch('/Patient/DeletePatientDocument', {
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

            // Reload docs for currently loaded patient (if ID present)
            const patientId = txtId ? txtId.value.trim() : null;
            if (patientId) {
                loadPatientDocuments(patientId);
            }
        } catch (err) {
            console.error(err);
            alert('An unexpected error occurred while deleting document.');
        }
    }

    function attachTabHandlers() {
        const tabs = document.querySelectorAll('#patientStageTabs .nav-link');
        tabs.forEach(tab => {
            tab.addEventListener('shown.bs.tab', function(e) {
                const stage = e.target.getAttribute('data-stage');
                if (stage) {
                    loadPatients(stage);
                }
            });
        });
    }

    function attachRowActionHandlers() {
        document.addEventListener('click', function(e) {
            const target = e.target;

            const editBtn = target.closest('.btn-patient-edit');
            if (editBtn) {
                const id = editBtn.getAttribute('data-id');
                if (id) {
                    openEditPatientModal(id);
                }
            }

            const deleteBtn = target.closest('.btn-patient-delete');
            if (deleteBtn) {
                const id = deleteBtn.getAttribute('data-id');
                if (id) {
                    deletePatient(id);
                }
            }

            const docDeleteBtn = target.closest('.btn-doc-delete');
            if (docDeleteBtn) {
                const idStr = docDeleteBtn.getAttribute('data-id');
                const docId = idStr ? parseInt(idStr, 10) : 0;
                if (docId > 0) {
                    deletePatientDocument(docId);
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function() {
        attachTabHandlers();
        attachRowActionHandlers();
        loadBranches();

        // Initial load for all stages
        stages.forEach(s => loadPatients(s));

        if (btnAdd) {
            btnAdd.addEventListener('click', function() {
                loadBranches().then(() => {
                    openAddPatientModal();
                });
            });
        }

        if (btnSave) {
            btnSave.addEventListener('click', savePatient);
        }

        if (txtSearch) {
        txtSearch.addEventListener('input', function () {
            applyPatientSearchFilter(); // filters current tab
            });
        }
    });
})(); 