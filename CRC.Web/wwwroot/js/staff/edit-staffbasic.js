// @ts-nocheck
(function () {
    let documentsContainer;
    let documentsMsg;
    let docTypesCache = [];
    let existingDocsCache = [];
    let mandatoryDocTypeIds = new Set();

    // Existing documents are NOT deleted immediately.
    // They are only deleted when user clicks Save and the overall save succeeds.
    let pendingDeleteDocIds = [];

    function getStaffId() {
        const root = document.querySelector('[data-staff-id]');
        return root ? (root.getAttribute('data-staff-id') || '') : '';
    }

    function setStaffId(newId) {
        const root = document.querySelector('[data-staff-id]');
        const hidden = document.getElementById('StaffIdHidden');
        const txtId = document.getElementById('StaffId');
        if (root && newId) {
            root.setAttribute('data-staff-id', newId);
        }
        if (hidden) hidden.value = newId || '';
        if (txtId) txtId.value = newId || '';
    }

    function setSelectOptions(select, items, valueField, textField, placeholder) {
        if (!select) return;
        select.innerHTML = '';

        if (placeholder) {
            const opt = document.createElement('option');
            opt.value = '';
            opt.textContent = placeholder;
            select.appendChild(opt);
        }

        items.forEach(item => {
            const opt = document.createElement('option');
            opt.value = item[valueField] || '';
            opt.textContent = item[textField] || '';
            select.appendChild(opt);
        });

        if (window.$ && $.fn.select2 && $(select).hasClass('select2-hidden-accessible')) {
            $(select).trigger('change.select2');
        }
    }

    function setSelectDisabled(select, isDisabled) {
        if (!select) return;
        select.disabled = !!isDisabled;
        if (window.$ && $.fn.select2 && $(select).hasClass('select2-hidden-accessible')) {
            $(select).prop('disabled', !!isDisabled).trigger('change.select2');
        }
    }

    // When Select2 is applied, setting `select.value` alone does not update the UI.
    // Select2 listens on `change.select2`, so we trigger that to refresh the displayed selection.
    function refreshSelect2(select) {
        if (!select) return;
        if (window.$ && $.fn.select2 && $(select).hasClass('select2-hidden-accessible')) {
            $(select).trigger('change.select2');
        }
    }

    function selectOptionByText(select, text) {
        if (!select || !text) return;
        const target = text.trim().toLowerCase();

        for (let i = 0; i < select.options.length; i++) {
            const opt = select.options[i];
            if ((opt.textContent || '').trim().toLowerCase() === target) {
                select.value = opt.value;
                refreshSelect2(select);
                return;
            }
        }
    }

    function getSelectedText(select) {
        if (!select || select.selectedIndex < 0) return '';
        return select.options[select.selectedIndex].text || '';
    }

    function formatDate(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    function parseBirthDateFromNric(nric) {
        const digits = (nric || '').replace(/\D/g, '');
        if (digits.length < 6) return null;

        const yy = parseInt(digits.substring(0, 2), 10);
        const mm = parseInt(digits.substring(2, 4), 10);
        const dd = parseInt(digits.substring(4, 6), 10);

        if (!yy || !mm || !dd || mm > 12 || dd > 31) return null;

        const currentYear = new Date().getFullYear();
        const currentYearTwoDigits = currentYear % 100;
        const fullYear = yy <= currentYearTwoDigits ? 2000 + yy : 1900 + yy;

        const date = new Date(fullYear, mm - 1, dd);
        if (isNaN(date.getTime())) return null;

        return date;
    }

    function computeAgeFromBirthDateInput() {
        const birthInput = document.getElementById('StaffBirthDate');
        const ageInput = document.getElementById('StaffAge');
        if (!birthInput || !ageInput) return;

        const v = birthInput.value;
        if (!v) {
            ageInput.value = '';
            return;
        }

        const dob = new Date(v);
        if (isNaN(dob.getTime())) {
            ageInput.value = '';
            return;
        }

        const today = new Date();
        let age = today.getFullYear() - dob.getFullYear();
        const m = today.getMonth() - dob.getMonth();
        if (m < 0 || (m === 0 && today.getDate() < dob.getDate())) {
            age--;
        }

        if (!isNaN(age)) {
            ageInput.value = age;
        }
    }

    function updateBirthDateFromNric() {
        const nricInput = document.getElementById('StaffNRIC');
        const birthInput = document.getElementById('StaffBirthDate');
        if (!nricInput || !birthInput) return;

        const nric = nricInput.value || '';
        const date = parseBirthDateFromNric(nric);
        if (!date) return;

        birthInput.value = formatDate(date);
        computeAgeFromBirthDateInput();
    }

    function updateAddressLineInputsEnabled() {
        const postcodeSelect = document.getElementById('StaffResPostcode');
        const addLine1 = document.getElementById('StaffAddLine1');
        const addLine2 = document.getElementById('StaffAddLine2');

        const enabled = !!(postcodeSelect && postcodeSelect.value);

        if (addLine1) addLine1.disabled = !enabled;
        if (addLine2) addLine2.disabled = !enabled;
    }

    function applySelect2() {
        if (!(window.$ && $.fn.select2)) return;

        const select2Config = {
            theme: 'bootstrap-5',
            width: '100%',
            allowClear: true
        };

        const stateSelect = $('#StaffResState');
        const citySelect = $('#StaffResCity');
        const postcodeSelect = $('#StaffResPostcode');

        if (stateSelect.length) {
            if (stateSelect.hasClass('select2-hidden-accessible')) {
                stateSelect.select2('destroy');
            }
            stateSelect.select2({
                ...select2Config,
                placeholder: '-- Select State --'
            });
        }

        if (citySelect.length) {
            if (citySelect.hasClass('select2-hidden-accessible')) {
                citySelect.select2('destroy');
            }
            citySelect.select2({
                ...select2Config,
                placeholder: '-- Select City --'
            });
        }

        if (postcodeSelect.length) {
            if (postcodeSelect.hasClass('select2-hidden-accessible')) {
                postcodeSelect.select2('destroy');
            }
            postcodeSelect.select2({
                ...select2Config,
                placeholder: '-- Select Postcode --'
            });
        }
    }

    function updateStaffTypeEditable(staffId) {
        const staffTypeSelect = document.getElementById('StaffType');
        const isExisting = !!staffId;

        setSelectDisabled(staffTypeSelect, isExisting);
    }

    async function loadLookups() {
        const msg = document.getElementById('staffBasicMessage');

        try {
            const response = await fetch('/Staff/GetStaffLookups', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                if (msg) msg.textContent = 'Error loading staff lookups.';
                return;
            }

            const result = await response.json();
            if (!result.success) {
                if (msg) msg.textContent = result.message || 'Error loading staff lookups.';
                return;
            }

            setSelectOptions(document.getElementById('StaffType'), result.staffTypes || [], 'staffTypeId', 'staffTypeName', '-- Select Staff Type --');
            setSelectOptions(document.getElementById('StaffResState'), result.states || [], 'id', 'name', '-- Select State --');
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'Error loading staff lookups.';
        }
    }

    async function loadCitiesByState(stateId) {
        const citySelect = document.getElementById('StaffResCity');
        const postcodeSelect = document.getElementById('StaffResPostcode');

        if (!citySelect) return;

        setSelectDisabled(citySelect, true);
        setSelectOptions(citySelect, [], 'id', 'name', '-- Select City --');
        if (postcodeSelect) {
            setSelectDisabled(postcodeSelect, true);
            setSelectOptions(postcodeSelect, [], 'id', 'name', '-- Select Postcode --');
        }
        updateAddressLineInputsEnabled();

        if (!stateId) return;

        try {
            const response = await fetch('/Staff/GetCitiesByState?stateId=' + encodeURIComponent(stateId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                return;
            }

            setSelectOptions(citySelect, result.cities || [], 'id', 'name', '-- Select City --');
            setSelectDisabled(citySelect, false);
        } catch (err) {
            console.error(err);
        }
    }

    async function loadPostcodesByCity(cityId) {
        const postcodeSelect = document.getElementById('StaffResPostcode');
        if (!postcodeSelect) return;

        setSelectDisabled(postcodeSelect, true);
        setSelectOptions(postcodeSelect, [], 'id', 'name', '-- Select Postcode --');
        updateAddressLineInputsEnabled();

        if (!cityId) return;

        try {
            const response = await fetch('/Staff/GetPostcodesByCity?cityId=' + encodeURIComponent(cityId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                return;
            }

            setSelectOptions(postcodeSelect, result.postcodes || [], 'id', 'name', '-- Select Postcode --');
            setSelectDisabled(postcodeSelect, false);
        } catch (err) {
            console.error(err);
        }
    }

    async function loadStaffBasic(staffId) {
        const msg = document.getElementById('staffBasicMessage');
        const headerName = document.getElementById('staffHeaderName');
        const headerId = document.getElementById('staffHeaderId');

        if (!staffId) {
            if (headerName) headerName.textContent = 'Staff: -';
            return;
        }

        try {
            const response = await fetch('/Staff/GetStaff?staffId=' + encodeURIComponent(staffId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                if (msg) msg.textContent = 'Error loading staff details.';
                return;
            }

            const result = await response.json();
            if (!result.success) {
                if (msg) msg.textContent = result.message || 'Error loading staff details.';
                return;
            }

            const s = result.data;
            if (!s) return;

            const txtId = document.getElementById('StaffId');
            const txtName = document.getElementById('StaffName');
            const txtNRIC = document.getElementById('StaffNRIC');
            const txtBirthDate = document.getElementById('StaffBirthDate');
            const txtAge = document.getElementById('StaffAge');
            const txtPhone = document.getElementById('StaffPhone');
            const txtEmail = document.getElementById('StaffEmail');
            const selGender = document.getElementById('StaffGender');
            const selState = document.getElementById('StaffResState');
            const selCity = document.getElementById('StaffResCity');
            const selPostcode = document.getElementById('StaffResPostcode');
            const txtAddLine1 = document.getElementById('StaffAddLine1');
            const txtAddLine2 = document.getElementById('StaffAddLine2');
            const txtBase = document.getElementById('StaffBase');
            const selType = document.getElementById('StaffType');

            if (txtId) txtId.value = s.staffId || '';
            if (txtName) txtName.value = s.name || '';
            if (txtNRIC) txtNRIC.value = s.nric || '';
            if (txtBirthDate) txtBirthDate.value = s.birthDate || '';
            if (txtAge) txtAge.value = s.age || '';
            if (txtPhone) txtPhone.value = s.phone || '';
            if (txtEmail) txtEmail.value = s.email || '';
            if (selGender) selGender.value = s.gender || '';
            if (txtAddLine1) txtAddLine1.value = s.addLine1 || '';
            if (txtAddLine2) txtAddLine2.value = s.addLine2 || '';
            if (txtBase) txtBase.value = s.staffBase || '';
            if (selType) selType.value = s.staffTypeId || '';

            if (selState) {
                selectOptionByText(selState, s.resState || '');
                const stateId = selState.value;
                if (stateId) {
                    await loadCitiesByState(stateId);
                }
            }

            if (selCity) {
                selectOptionByText(selCity, s.resCity || '');
                const cityId = selCity.value;
                if (cityId) {
                    await loadPostcodesByCity(cityId);
                }
            }

            if (selPostcode) {
                selectOptionByText(selPostcode, s.resPostcode || '');
            }

            updateAddressLineInputsEnabled();
            updateStaffTypeEditable(s.staffId);

            await refreshMandatoryDocuments();
            await reloadDocumentsSection();

            if (headerName) headerName.textContent = 'Staff: ' + (s.name || '-');
            if (headerId && s.staffId) headerId.textContent = 'ID: ' + s.staffId;
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'Error loading staff details.';
        }
    }

    // ----- Documents (Documents Section under Basic Details) -----

    function showDocumentsMessage(text, isError) {
        if (!documentsMsg) return;
        documentsMsg.textContent = text || '';
        documentsMsg.classList.remove('text-success', 'text-danger');
        documentsMsg.classList.add(isError ? 'text-danger' : 'text-success');
    }

    function clearDocumentsMessage() {
        if (!documentsMsg) return;
        documentsMsg.textContent = '';
        documentsMsg.classList.remove('text-success', 'text-danger');
    }

    async function loadDocumentTypes() {
        try {
            const response = await fetch('/Staff/GetStaffDocumentTypes', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                showDocumentsMessage('Error loading document types.', true);
                return [];
            }

            const result = await response.json();
            if (!result.success) {
                showDocumentsMessage(result.message || 'Error loading document types.', true);
                return [];
            }

            return result.data || [];
        } catch (err) {
            console.error(err);
            showDocumentsMessage('Error loading document types.', true);
            return [];
        }
    }

    async function loadExistingDocuments(staffId) {
        if (!staffId) return [];

        try {
            const response = await fetch('/Staff/GetStaffDocuments?staffId=' + encodeURIComponent(staffId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) return [];
            const result = await response.json();
            if (!result.success) return [];

            return result.data || [];
        } catch (err) {
            console.error(err);
            return [];
        }
    }

    async function refreshMandatoryDocuments() {
        mandatoryDocTypeIds = new Set();

        const staffTypeSelect = document.getElementById('StaffType');
        const staffTypeId = staffTypeSelect?.value || '';
        if (!staffTypeId) {
            return;
        }

        try {
            const response = await fetch('/Staff/GetMandatoryDocumentsForStaffType?staffTypeId=' + encodeURIComponent(staffTypeId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) return;
            const result = await response.json();
            if (!result.success) return;

            const list = result.data || [];
            list.forEach(d => {
                const id = d.staffDocumentTypeId || '';
                if (id) mandatoryDocTypeIds.add(id);
            });
        } catch (err) {
            console.error(err);
        }
    }

    function getDocTypeNameFromCard(card) {
        return card.getAttribute('data-doc-type-name') || '';
    }

    function renderDocumentCards(docTypes, existingDocs) {
        if (!documentsContainer) return;

        clearDocumentsMessage();

        if (!docTypes || docTypes.length === 0) {
            documentsContainer.innerHTML = `
                <p class="text-muted mb-0">No staff document types configured.</p>
            `;
            return;
        }

        const pendingSet = new Set((pendingDeleteDocIds || []).map(x => parseInt(x, 10)).filter(x => x > 0));

        // Hide documents that are marked for deletion (until Save)
        const visibleExistingDocs = (existingDocs || []).filter(d => !pendingSet.has(parseInt(d.documentId, 10)));

        const existingByType = new Map();
        visibleExistingDocs.forEach(d => {
            const typeId = d.staffDocumentTypeId || '';
            if (!typeId) return;
            if (!existingByType.has(typeId)) existingByType.set(typeId, []);
            existingByType.get(typeId).push(d);
        });

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

            const isMandatory = mandatoryDocTypeIds.has(docTypeId);
            const existingForType = existingByType.get(docTypeId) || [];

            body.innerHTML = `
                <div class="d-flex align-items-start justify-content-between gap-2">
                    <h6 class="card-title mb-2">${docTypeName}</h6>
                    ${isMandatory ? '<span class="badge bg-warning text-dark">Mandatory</span>' : ''}
                </div>

                <div class="mb-2">
                    <input type="file"
                           class="form-control form-control-sm staff-doc-file"
                           multiple />
                    <div class="form-text selected-files">No new files selected.</div>
                </div>

                <div class="small text-muted mb-1">Existing documents:</div>
                <div class="staff-doc-list">
                    <p class="text-muted mb-0">No documents uploaded.</p>
                </div>
            `;

            card.appendChild(body);
            col.appendChild(card);
            wrapper.appendChild(col);

            // Existing documents
            const listDiv = body.querySelector('.staff-doc-list');
            if (listDiv && existingForType.length > 0) {
                const ul = document.createElement('ul');
                ul.className = 'list-group mb-0';

                existingForType.forEach(d => {
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

                listDiv.innerHTML = '';
                listDiv.appendChild(ul);
            }
        });

        documentsContainer.innerHTML = '';
        documentsContainer.appendChild(wrapper);
    }

    async function reloadDocumentsSection() {
        if (!documentsContainer) return;

        documentsContainer.innerHTML = '<p class="text-muted mb-0">Loading document types...</p>';

        if (!docTypesCache || docTypesCache.length === 0) {
            docTypesCache = await loadDocumentTypes();
        }

        existingDocsCache = await loadExistingDocuments(getStaffId());
        renderDocumentCards(docTypesCache, existingDocsCache);
    }

    function markDocumentForDeletion(documentId) {
        if (!confirm('Are you sure you want to delete this document?')) {
            return;
        }

        const id = parseInt(documentId, 10) || 0;
        if (id <= 0) return;

        if (!pendingDeleteDocIds.includes(id)) {
            pendingDeleteDocIds.push(id);
        }

        // Hide it immediately from the UI, but do not delete on server yet.
        const li = document.querySelector(`.staff-doc-list [data-doc-id="${id}"]`);
        if (li) {
            li.remove();
        }

        showDocumentsMessage('Document will be deleted when you click Save.', false);
    }

    function attachDocumentsHandlers() {
        // Delete handler
        document.addEventListener('click', function (e) {
            const target = e.target;
            if (!target) return;

            const deleteBtn = target.closest('.btn-staff-doc-delete');
            if (deleteBtn) {
                const idStr = deleteBtn.getAttribute('data-id');
                const docId = idStr ? parseInt(idStr, 10) : 0;
                if (docId > 0) {
                    markDocumentForDeletion(docId);
                }
            }
        });

        // Show selected file(s)
        document.addEventListener('change', function (e) {
            const target = e.target;
            if (!target) return;

            if (target.classList && target.classList.contains('staff-doc-file')) {
                const card = target.closest('.card');
                const hint = card ? card.querySelector('.selected-files') : null;

                const files = target.files;
                if (!hint) return;

                if (!files || files.length === 0) {
                    hint.textContent = 'No new files selected.';
                    return;
                }

                if (files.length === 1) {
                    hint.textContent = `Selected: ${files[0].name}`;
                    return;
                }

                hint.textContent = `Selected: ${files.length} files`;
            }
        });

        // Mandatory docs refresh when Staff Type changes (for new staff)
        const staffTypeSelect = document.getElementById('StaffType');
        if (staffTypeSelect) {
            staffTypeSelect.addEventListener('change', async function () {
                await refreshMandatoryDocuments();
                await reloadDocumentsSection();
            });
        }
    }

    async function saveStaff() {
        const msg = document.getElementById('staffBasicMessage');
        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-success', 'text-danger');
        }

        const staffId = getStaffId();
        const isNew = !staffId;

        const txtName = document.getElementById('StaffName');
        const txtNRIC = document.getElementById('StaffNRIC');
        const txtBirthDate = document.getElementById('StaffBirthDate');
        const txtAge = document.getElementById('StaffAge');
        const txtPhone = document.getElementById('StaffPhone');
        const txtEmail = document.getElementById('StaffEmail');
        const selGender = document.getElementById('StaffGender');
        const selState = document.getElementById('StaffResState');
        const selCity = document.getElementById('StaffResCity');
        const selPostcode = document.getElementById('StaffResPostcode');
        const txtAddLine1 = document.getElementById('StaffAddLine1');
        const txtAddLine2 = document.getElementById('StaffAddLine2');
        const txtBase = document.getElementById('StaffBase');
        const selType = document.getElementById('StaffType');

        const payload = {
            isNew: isNew,
            staffId: staffId,
            name: (txtName?.value || '').trim().toUpperCase(),
            nric: (txtNRIC?.value || '').trim(),
            birthDate: (txtBirthDate?.value || '').trim(),
            age: parseInt(txtAge?.value || '0', 10) || 0,
            phone: (txtPhone?.value || '').trim(),
            email: (txtEmail?.value || '').trim(),
            gender: selGender?.value || '',
            resState: getSelectedText(selState),
            resCity: getSelectedText(selCity),
            resPostcode: getSelectedText(selPostcode),
            addLine1: (txtAddLine1?.value || '').trim(),
            addLine2: (txtAddLine2?.value || '').trim(),
            staffBase: (txtBase?.value || '').trim(),
            staffTypeId: selType?.value || ''
        };

        if (!payload.name || !payload.nric || !payload.birthDate || payload.age <= 0 ||
            !payload.phone || !payload.email || !payload.gender || !payload.resState ||
            !payload.resCity || !payload.resPostcode || !payload.addLine1 ||
            !payload.addLine2 || !payload.staffBase || !payload.staffTypeId) {
            if (msg) {
                msg.textContent = 'Please fill in all required fields.';
                msg.classList.add('text-danger');
            }
            return;
        }

        try {
            // Build multipart payload (staff + documents)
            const formData = new FormData();
            formData.append('IsNew', String(isNew));
            formData.append('StaffId', payload.staffId || '');
            formData.append('Name', payload.name || '');
            formData.append('NRIC', payload.nric || '');
            formData.append('BirthDate', payload.birthDate || '');
            formData.append('Age', String(payload.age || 0));
            formData.append('Phone', payload.phone || '');
            formData.append('Email', payload.email || '');
            formData.append('Gender', payload.gender || '');
            formData.append('ResState', payload.resState || '');
            formData.append('ResCity', payload.resCity || '');
            formData.append('ResPostcode', payload.resPostcode || '');
            formData.append('AddLine1', payload.addLine1 || '');
            formData.append('AddLine2', payload.addLine2 || '');
            formData.append('StaffBase', payload.staffBase || '');
            formData.append('StaffTypeId', payload.staffTypeId || '');

            // Add files selected in Documents Section
            const cards = document.querySelectorAll('#staffDocumentsContainer .card[data-doc-type-id]');
            cards.forEach(card => {
                const docTypeId = card.getAttribute('data-doc-type-id') || '';
                const docTypeName = getDocTypeNameFromCard(card);
                const input = card.querySelector('.staff-doc-file');
                if (!input || !input.files || input.files.length === 0) return;

                for (let i = 0; i < input.files.length; i++) {
                    const file = input.files[i];
                    formData.append('files', file);
                    formData.append('docTypeIds', docTypeId);
                    formData.append('docTypeNames', docTypeName);
                }
            });

            // Pending deletions (existing documents)
            (pendingDeleteDocIds || []).forEach(id => {
                formData.append('deleteDocIds', String(id));
            });

            const response = await fetch('/Staff/SaveStaffWithDocuments', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                if (msg) {
                    msg.textContent = 'Server error while saving staff.';
                    msg.classList.add('text-danger');
                }
                return;
            }

            const result = await response.json();

            if (result.staffId) {
                setStaffId(result.staffId);
            }

            const headerName = document.getElementById('staffHeaderName');
            const headerId = document.getElementById('staffHeaderId');
            if (headerName) headerName.textContent = 'Staff: ' + (payload.name || '-');
            if (headerId && result.staffId) headerId.textContent = 'ID: ' + result.staffId;

            if (!result.success) {
                if (msg) {
                    msg.textContent = result.message || 'Failed to save staff.';
                    msg.classList.add('text-danger');
                }

                // If save failed, nothing should have been committed.
                // For existing staff, refresh documents so UI reflects server state.
                // For new staff, keep the selected files (do NOT re-render).
                clearDocumentsMessage();
                if (result.message) showDocumentsMessage(result.message, true);

                pendingDeleteDocIds = [];

                if (!isNew || (result.staffId && result.staffId !== '')) {
                    await reloadDocumentsSection();
                }
                return;
            }

            if (msg) {
                msg.textContent = '';
                msg.classList.remove('text-success', 'text-danger');
            }

            // Show the "Saved Successfully" modal
            const modalEl = document.getElementById('saveSuccessModal');
            if (modalEl && window.bootstrap && bootstrap.Modal) {
                const saveModal = bootstrap.Modal.getOrCreateInstance(modalEl);
                saveModal.show();
            }

            updateStaffTypeEditable(result.staffId || staffId);

            // Clear pending deletions after successful save
            pendingDeleteDocIds = [];

            // Clear file inputs after successful save
            const fileInputs = document.querySelectorAll('#staffDocumentsContainer .staff-doc-file');
            fileInputs.forEach(i => { try { i.value = ''; } catch { } });
            await reloadDocumentsSection();
        } catch (err) {
            console.error(err);
            if (msg) {
                msg.textContent = 'An unexpected error occurred.';
                msg.classList.add('text-danger');
            }
        }
    }

    document.addEventListener('DOMContentLoaded', async function () {
        await loadLookups();
        applySelect2();

        documentsContainer = document.getElementById('staffDocumentsContainer');
        documentsMsg = document.getElementById('staffDocumentsMessage');
        attachDocumentsHandlers();
        await refreshMandatoryDocuments();
        await reloadDocumentsSection();

        const staffId = getStaffId();
        if (staffId) {
            setStaffId(staffId);
        }

        await loadStaffBasic(staffId);
        updateStaffTypeEditable(staffId);

        const nricInput = document.getElementById('StaffNRIC');
        if (nricInput) {
            nricInput.addEventListener('blur', updateBirthDateFromNric);
        }

        if (window.$ && $.fn.select2) {
            $('#StaffResState')
                .off('change.staffbasic')
                .on('change.staffbasic', async function () {
                    await loadCitiesByState(this.value);
                });

            $('#StaffResCity')
                .off('change.staffbasic')
                .on('change.staffbasic', async function () {
                    await loadPostcodesByCity(this.value);
                });

            $('#StaffResPostcode')
                .off('change.staffbasic')
                .on('change.staffbasic', updateAddressLineInputsEnabled);
        } else {
            const stateSelect = document.getElementById('StaffResState');
            if (stateSelect) {
                stateSelect.addEventListener('change', async function () {
                    await loadCitiesByState(stateSelect.value);
                });
            }

            const citySelect = document.getElementById('StaffResCity');
            if (citySelect) {
                citySelect.addEventListener('change', async function () {
                    await loadPostcodesByCity(citySelect.value);
                });
            }

            const postcodeSelect = document.getElementById('StaffResPostcode');
            if (postcodeSelect) {
                postcodeSelect.addEventListener('change', updateAddressLineInputsEnabled);
            }
        }

        const btnSave = document.getElementById('btnSaveStaffMain');
        if (btnSave) {
            btnSave.addEventListener('click', saveStaff);
        }
    });
})();