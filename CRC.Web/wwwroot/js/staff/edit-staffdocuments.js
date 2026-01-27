// @ts-nocheck
(function() {
    let container;
    let msg;

    function getStaffId() {
        const root = document.querySelector('[data-staff-id]');
        return root ? (root.getAttribute('data-staff-id') || '') : '';
    }

    function getStaffName() {
        const input = document.getElementById('StaffName');
        return input ? (input.value || '') : '';
    }

    function showMessage(text, isError) {
        if (!msg) return;
        msg.textContent = text || '';
        msg.classList.remove('text-success', 'text-danger');
        msg.classList.add(isError ? 'text-danger' : 'text-success');
    }

    function clearMessage() {
        if (!msg) return;
        msg.textContent = '';
        msg.classList.remove('text-success', 'text-danger');
    }

    function renderCards(docTypes, existingDocs, staffSaved) {
        if (!container) return;

        if (!staffSaved) {
            container.innerHTML = `
                <p class="text-muted mb-0">
                    Please save Basic Details first before uploading documents.
                </p>
            `;
            return;
        }

        if (!docTypes || docTypes.length === 0) {
            container.innerHTML = `
                <p class="text-muted mb-0">
                    No staff document types configured.
                </p>
            `;
            return;
        }

        const wrapper = document.createElement('div');
        wrapper.className = 'row g-3';

        docTypes.forEach(t => {
            const docTypeId = t.documentTypeId || '';
            const docTypeName = t.documentTypeName || '';

            const col = document.createElement('div');
            col.className = 'col-md-6';

            const card = document.createElement('div');
            card.className = 'card h-100';
            card.setAttribute('data-doc-type-id', docTypeId);
            card.setAttribute('data-doc-type-name', docTypeName);

            const body = document.createElement('div');
            body.className = 'card-body';

            body.innerHTML = `
                <h6 class="card-title mb-2">
                    ${docTypeName}
                </h6>
                <div class="mb-2">
                    <input type="file"
                           class="form-control form-control-sm staff-doc-file"
                           multiple />
                </div>
                <div class="mb-2">
                    <button type="button"
                            class="btn btn-sm btn-primary btn-staff-doc-upload">
                        Upload
                    </button>
                </div>
                <div class="small text-muted mb-1">
                    Existing documents:
                </div>
                <div class="staff-doc-list">
                    <p class="text-muted mb-0">No documents uploaded.</p>
                </div>
            `;

            card.appendChild(body);
            col.appendChild(card);
            wrapper.appendChild(col);
        });

        container.innerHTML = '';
        container.appendChild(wrapper);

        if (existingDocs && existingDocs.length > 0) {
            existingDocs.forEach(d => {
                const typeId = d.staffDocumentTypeId || '';
                const card = container.querySelector(
                    `.card[data-doc-type-id="${CSS.escape(typeId)}"]`
                );

                if (!card) return;

                const listDiv = card.querySelector('.staff-doc-list');
                if (!listDiv) return;

                let ul = listDiv.querySelector('ul');
                if (!ul) {
                    ul = document.createElement('ul');
                    ul.className = 'list-group mb-0';
                    listDiv.innerHTML = '';
                    listDiv.appendChild(ul);
                }

                const li = document.createElement('li');
                li.className = 'list-group-item d-flex justify-content-between align-items-center';
                li.setAttribute('data-doc-id', d.documentId);

                const safeName = d.fileName || '';
                const safePath = d.filePath || '#';
                const uploaded = d.uploadedOn || '';

                const left = document.createElement('div');
                left.innerHTML = `
                    <a href="${safePath}" target="_blank" rel="noopener noreferrer">
                        ${safeName}
                    </a>
                    <br />
                    <small class="text-muted">${uploaded}</small>
                `;

                const right = document.createElement('div');
                right.innerHTML = `
                    <button type="button"
                            class="btn btn-sm btn-outline-danger btn-staff-doc-delete"
                            data-id="${d.documentId}">
                        Delete
                    </button>
                `;

                li.appendChild(left);
                li.appendChild(right);
                ul.appendChild(li);
            });
        }
    }

    async function loadTypesAndDocs() {
        if (!container) return;

        const staffId = getStaffId();
        const staffSaved = !!staffId;

        clearMessage();
        container.innerHTML = '<p class="text-muted mb-0">Loading document types...</p>';

        try {
            const typesResponse = await fetch('/Staff/GetStaffDocumentTypes', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!typesResponse.ok) {
                container.innerHTML = '<p class="text-danger mb-0">Error loading document types.</p>';
                return;
            }

            const typesResult = await typesResponse.json();
            if (!typesResult.success) {
                container.innerHTML = `<p class="text-danger mb-0">${typesResult.message || 'Error loading document types.'}</p>`;
                return;
            }

            const docTypes = typesResult.data || [];

            let existingDocs = [];
            if (staffSaved) {
                const docsResponse = await fetch('/Staff/GetStaffDocuments?staffId=' + encodeURIComponent(staffId), {
                    method: 'GET',
                    headers: { 'Accept': 'application/json' }
                });

                if (docsResponse.ok) {
                    const docsResult = await docsResponse.json();
                    if (docsResult.success) {
                        existingDocs = docsResult.data || [];
                    }
                }
            }

            renderCards(docTypes, existingDocs, staffSaved);
        } catch (err) {
            console.error('Error loading types/documents', err);
            container.innerHTML = '<p class="text-danger mb-0">Error loading documents.</p>';
        }
    }

    async function uploadDocumentsForCard(card) {
        const staffId = getStaffId();
        if (!staffId) {
            showMessage('Please save Basic Details first before uploading documents.', true);
            return;
        }

        const staffName = getStaffName() || '';
        const docTypeId = card.getAttribute('data-doc-type-id') || '';
        const docTypeName = card.getAttribute('data-doc-type-name') || '';
        const input = card.querySelector('.staff-doc-file');

        if (!input || !input.files || input.files.length === 0) {
            showMessage(`Please choose file(s) to upload for: ${docTypeName}.`, true);
            return;
        }

        clearMessage();

        const formData = new FormData();
        formData.append('staffId', staffId);
        formData.append('staffName', staffName);

        for (let i = 0; i < input.files.length; i++) {
            const file = input.files[i];
            formData.append('files', file);
            formData.append('docTypeIds', docTypeId);
            formData.append('docTypeNames', docTypeName);
        }

        try {
            const response = await fetch('/Staff/UploadStaffDocuments', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                showMessage('Server error while uploading documents.', true);
                return;
            }

            const result = await response.json();

            if (!result.success) {
                showMessage(result.message || 'Failed to upload documents.', true);
                return;
            }

            input.value = '';
            showMessage('Documents uploaded successfully.', false);

            await loadTypesAndDocs();
        } catch (err) {
            console.error('Error uploading staff documents', err);
            showMessage('An unexpected error occurred while uploading documents.', true);
        }
    }

    async function deleteDocument(documentId) {
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
                body: JSON.stringify({ documentId })
            });

            if (!response.ok) {
                showMessage('Server error while deleting document.', true);
                return;
            }

            const result = await response.json();

            if (!result.success) {
                showMessage(result.message || 'Failed to delete document.', true);
                return;
            }

            showMessage('Document deleted successfully.', false);
            await loadTypesAndDocs();
        } catch (err) {
            console.error('Error deleting staff document', err);
            showMessage('An unexpected error occurred while deleting documents.', true);
        }
    }

    function attachHandlers() {
        document.addEventListener('click', function(e) {
            const target = e.target;
            if (!target) return;

            const uploadBtn = target.closest('.btn-staff-doc-upload');
            if (uploadBtn) {
                const card = uploadBtn.closest('.card');
                if (card) {
                    uploadDocumentsForCard(card);
                }
            }

            const deleteBtn = target.closest('.btn-staff-doc-delete');
            if (deleteBtn) {
                const idStr = deleteBtn.getAttribute('data-id');
                const docId = idStr ? parseInt(idStr, 10) : 0;
                if (docId > 0) {
                    deleteDocument(docId);
                }
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function() {
        container = document.getElementById('staffDocumentsContainer');
        msg = document.getElementById('staffDocumentsMessage');

        loadTypesAndDocs();
        attachHandlers();

        window.StaffDocumentsTab = {
            reload: loadTypesAndDocs
        };
    });
})();
