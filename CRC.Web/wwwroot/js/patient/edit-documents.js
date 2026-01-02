// @ts-nocheck
(function() {
    let container;
    let msg;

    function getPatientId() {
        const root = document.querySelector('[data-patient-id]');
        return root ? (root.getAttribute('data-patient-id') || '') : '';
    }

    function getPatientName() {
        const input = document.getElementById('PatientName');
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

    function renderCards(docTypes, existingDocs, patientSaved) {
        if (!container) return;

        if (!patientSaved) {
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
                    No patient document types configured.
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
                           class="form-control form-control-sm pat-doc-file"
                           multiple />
                </div>
                <div class="mb-2">
                    <button type="button"
                            class="btn btn-sm btn-primary btn-pat-doc-upload">
                        Upload
                    </button>
                </div>
                <div class="small text-muted mb-1">
                    Existing documents:
                </div>
                <div class="pat-doc-list">
                    <p class="text-muted mb-0">No documents uploaded.</p>
                </div>
            `;

            card.appendChild(body);
            col.appendChild(card);
            wrapper.appendChild(col);
        });

        container.innerHTML = '';
        container.appendChild(wrapper);

        // After cards are in DOM, populate existing docs by type
        if (existingDocs && existingDocs.length > 0) {
            existingDocs.forEach(d => {
                const typeId = d.docTypeId || '';
                const card = container.querySelector(
                    `.card[data-doc-type-id="${CSS.escape(typeId)}"]`
                );

                if (!card) return;

                const listDiv = card.querySelector('.pat-doc-list');
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
                            class="btn btn-sm btn-outline-danger btn-pat-doc-delete"
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

        const patientId = getPatientId();
        const patientSaved = !!patientId;

        clearMessage();
        container.innerHTML = '<p class="text-muted mb-0">Loading document types...</p>';

        try {
            // 1) get types
            const typesResponse = await fetch('/Patient/GetPatientDocumentTypes', {
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

            // 2) if patient saved, load existing docs
            let existingDocs = [];
            if (patientSaved) {
                const docsResponse = await fetch('/Patient/GetPatientDocuments?patientId=' + encodeURIComponent(patientId), {
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

            renderCards(docTypes, existingDocs, patientSaved);
        } catch (err) {
            console.error('Error loading types/documents', err);
            container.innerHTML = '<p class="text-danger mb-0">Error loading documents.</p>';
        }
    }

    async function uploadDocumentsForCard(card) {
        const patientId = getPatientId();
        if (!patientId) {
            showMessage('Please save Basic Details first before uploading documents.', true);
            return;
        }

        const patientName = getPatientName() || '';
        const docTypeId = card.getAttribute('data-doc-type-id') || '';
        const docTypeName = card.getAttribute('data-doc-type-name') || '';
        const input = card.querySelector('.pat-doc-file');

        if (!input || !input.files || input.files.length === 0) {
            showMessage(`Please choose file(s) to upload for: ${docTypeName}.`, true);
            return;
        }

        clearMessage();

        const formData = new FormData();
        formData.append('patientId', patientId);
        formData.append('patientName', patientName);

        for (let i = 0; i < input.files.length; i++) {
            const file = input.files[i];
            formData.append('files', file);
            formData.append('docTypeIds', docTypeId);
            formData.append('docTypeNames', docTypeName);
        }

        try {
            const response = await fetch('/Patient/UploadPatientDocuments', {
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

            // Reload docs to refresh lists
            await loadTypesAndDocs();
        } catch (err) {
            console.error('Error uploading patient documents', err);
            showMessage('An unexpected error occurred while uploading documents.', true);
        }
    }

    async function deleteDocument(docId) {
        if (!docId || docId <= 0) return;

        if (!confirm('Are you sure you want to delete this document?')) return;

        try {
            const response = await fetch('/Patient/DeletePatientDocument', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ documentId: docId })
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

            await loadTypesAndDocs();
        } catch (err) {
            console.error('Error deleting patient document', err);
            alert('An unexpected error occurred while deleting document.');
        }
    }

    function attachHandlers() {
        document.addEventListener('click', function(e) {
            const uploadBtn = e.target.closest('.btn-pat-doc-upload');
            if (uploadBtn) {
                const card = uploadBtn.closest('.card');
                if (card) {
                    uploadDocumentsForCard(card);
                }
            }

            const deleteBtn = e.target.closest('.btn-pat-doc-delete');
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
        container = document.getElementById('patientDocumentsContainer');
        msg = document.getElementById('documentsMessage');

        attachHandlers();
        loadTypesAndDocs();
    });

    // 🔹 Public API for other tabs
    window.PatientDocumentsTab = {
        reload: function() {
            loadTypesAndDocs();
        }
    };
})();