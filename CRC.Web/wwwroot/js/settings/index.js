// @ts-nocheck
(function() {
    const selStaffType = document.getElementById('SettingsStaffType');
    const docList = document.getElementById('documentTypeList');
    const msg = document.getElementById('settingsMessage');
    const btnSave = document.getElementById('btnSaveSettings');
    const selDischargeType = document.getElementById('SettingsDischargeType');
    const dischargeDocList = document.getElementById('dischargeDocumentTypeList');
    const dischargeMsg = document.getElementById('dischargeSettingsMessage');
    const btnSaveDischarge = document.getElementById('btnSaveDischargeSettings');

// ========= Mandatory Staff Documents ==========
    async function loadStaffTypes() {
        if (!selStaffType) return;

        selStaffType.innerHTML = '<option value="">Loading staff types...</option>';

        try {
            const response = await fetch('/Settings/GetStaffTypes', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                selStaffType.innerHTML = '<option value="">Error loading staff types</option>';
                return;
            }

            const data = await response.json();

            if (!data || data.length === 0) {
                selStaffType.innerHTML = '<option value="">No staff types found</option>';
                return;
            }

            selStaffType.innerHTML = '<option value="">-- Select Staff Type --</option>';

            data.forEach(t => {
                const opt = document.createElement('option');
                opt.value = t.staffTypeId;
                opt.textContent = t.staffTypeName;
                selStaffType.appendChild(opt);
            });
        } catch (err) {
            console.error('Error loading staff types', err);
            selStaffType.innerHTML = '<option value="">Error loading staff types</option>';
        }
    }

    async function loadStaffDocumentSettings(staffTypeId) {
        if (!docList) return;

        if (!staffTypeId) {
            docList.innerHTML = '<p class="text-muted mb-0">No staff type selected.</p>';
            if (msg) {
                msg.textContent = 'Please select a staff type to view mandatory documents.';
                msg.classList.remove('text-danger');
                msg.classList.add('text-muted');
            }
            return;
        }

        docList.innerHTML = '<p class="text-muted mb-0">Loading document types...</p>';

        try {
            const response = await fetch('/Settings/GetStaffDocumentSettings?staffTypeId=' + encodeURIComponent(staffTypeId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            const result = await response.json();

            if (!response.ok || !result.success) {
                docList.innerHTML = '<p class="text-danger mb-0">Error loading document settings.</p>';
                if (msg) {
                    msg.textContent = result.message || 'Error loading document settings.';
                    msg.classList.remove('text-muted');
                    msg.classList.add('text-danger');
                }
                return;
            }

            const data = result.data || [];

            if (data.length === 0) {
                docList.innerHTML = '<p class="text-muted mb-0">No document types found.</p>';
                if (msg) {
                    msg.textContent = 'No document types found for configuration.';
                    msg.classList.remove('text-danger');
                    msg.classList.add('text-muted');
                }
                return;
            }

            // Build a nice list of checkboxes
            const wrapper = document.createElement('div');
            wrapper.className = 'list-group';

            data.forEach(d => {
                const label = document.createElement('label');
                label.className = 'list-group-item d-flex align-items-center';

                const cb = document.createElement('input');
                cb.type = 'checkbox';
                cb.className = 'form-check-input me-2';
                cb.value = d.staffDocumentTypeId;

                if (d.isMandatory) {
                    cb.checked = true;
                }

                const span = document.createElement('span');
                span.textContent = d.staffDocumentTypeName;

                label.appendChild(cb);
                label.appendChild(span);

                wrapper.appendChild(label);
            });

            docList.innerHTML = '';

            const helper = document.createElement('p');
            helper.className = 'small text-muted mb-2';
            helper.textContent = 'Tick the documents that are mandatory for the selected staff type.';

            docList.appendChild(helper);
            docList.appendChild(wrapper);

            if (msg) {
                msg.textContent = 'Configure which documents are mandatory for this staff type.';
                msg.classList.remove('text-danger');
                msg.classList.add('text-muted');
            }
        } catch (err) {
            console.error('Error loading document settings', err);
            docList.innerHTML = '<p class="text-danger mb-0">Error loading document settings.</p>';
            if (msg) {
                msg.textContent = 'Error loading document settings.';
                msg.classList.remove('text-muted');
                msg.classList.add('text-danger');
            }
        }
    }

    function getSelectedDocumentTypeIds() {
        if (!docList) return [];

        const checkboxes = docList.querySelectorAll('input[type="checkbox"]');
        const ids = [];

        checkboxes.forEach(cb => {
            if (cb.checked && cb.value) {
                ids.push(cb.value);
            }
        });

        return ids;
    }

    async function saveSettings() {
        if (!selStaffType) return;

        const staffTypeId = selStaffType.value;
        const staffTypeName = selStaffType.options[selStaffType.selectedIndex]
            ? selStaffType.options[selStaffType.selectedIndex].textContent
            : '';

        if (!staffTypeId) {
            if (msg) {
                msg.textContent = 'Please select a staff type before saving.';
                msg.classList.remove('text-muted');
                msg.classList.add('text-danger');
            }
            return;
        }

        const docIds = getSelectedDocumentTypeIds();

        const payload = {
            staffTypeId: staffTypeId,
            staffTypeName: staffTypeName,
            documentTypeIds: docIds
        };

        try {
            const response = await fetch('/Settings/SaveStaffDocumentSettings', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                if (msg) {
                    msg.textContent = 'Server error while saving settings.';
                    msg.classList.remove('text-muted');
                    msg.classList.add('text-danger');
                }
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (msg) {
                    msg.textContent = result.message || 'Failed to save settings.';
                    msg.classList.remove('text-muted');
                    msg.classList.add('text-danger');
                }
                return;
            }

            if (msg) {
                msg.textContent = result.message || 'Settings saved successfully.';
                msg.classList.remove('text-danger');
                msg.classList.add('text-success');
            }
        } catch (err) {
            console.error('Error saving settings', err);
            if (msg) {
                msg.textContent = 'An unexpected error occurred.';
                msg.classList.remove('text-muted');
                msg.classList.add('text-danger');
            }
        }
    }

// ========= Mandatory Staff Documents ==========
    async function loadDischargeTypes() {
        if (!selDischargeType) return;

        selDischargeType.innerHTML = '<option value="">Loading discharge types...</option>';

        try {
            const response = await fetch('/Settings/GetDischargeTypes', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                selDischargeType.innerHTML = '<option value="">Error loading discharge types</option>';
                if (dischargeMsg) {
                    dischargeMsg.textContent = 'Error loading discharge types.';
                    dischargeMsg.classList.remove('text-success');
                    dischargeMsg.classList.add('text-danger');
                }
                return;
            }

            const result = await response.json();
            if (!result.success) {
                selDischargeType.innerHTML = '<option value="">Error loading discharge types</option>';
                if (dischargeMsg) {
                    dischargeMsg.textContent = result.message || 'Error loading discharge types.';
                    dischargeMsg.classList.remove('text-success');
                    dischargeMsg.classList.add('text-danger');
                }
                return;
            }

            const list = result.data || [];

            selDischargeType.innerHTML = '<option value="">-- Select Discharge Type --</option>';

            list.forEach(t => {
                const opt = document.createElement('option');
                opt.value = t.dischargeTypeId;
                opt.textContent = t.dischargeTypeName;
                selDischargeType.appendChild(opt);
            });

            if (dischargeMsg) {
                dischargeMsg.textContent = 'Select a discharge type to configure mandatory patient documents.';
                dischargeMsg.classList.remove('text-danger');
                dischargeMsg.classList.add('text-muted');
            }
        } catch (err) {
            console.error('Error loading discharge types', err);
            selDischargeType.innerHTML = '<option value="">Error loading discharge types</option>';
            if (dischargeMsg) {
                dischargeMsg.textContent = 'Error loading discharge types.';
                dischargeMsg.classList.remove('text-success');
                dischargeMsg.classList.add('text-danger');
            }
        }
    }

    async function loadDischargeDocumentSettings(dischargeTypeId) {
        if (!dischargeDocList) return;

        if (!dischargeTypeId) {
            dischargeDocList.innerHTML = '<p class="text-muted mb-0">No discharge type selected.</p>';
            if (dischargeMsg) {
                dischargeMsg.textContent = 'Please select a discharge type to view mandatory documents.';
                dischargeMsg.classList.remove('text-danger');
                dischargeMsg.classList.add('text-muted');
            }
            return;
        }

        dischargeDocList.innerHTML = '<p class="text-muted mb-0">Loading document types...</p>';

        try {
            // 1) Get all patient document types
            const typesResponse = await fetch('/Patient/GetPatientDocumentTypes', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!typesResponse.ok) {
                dischargeDocList.innerHTML = '<p class="text-danger mb-0">Error loading patient document types.</p>';
                if (dischargeMsg) {
                    dischargeMsg.textContent = 'Error loading patient document types.';
                    dischargeMsg.classList.remove('text-muted');
                    dischargeMsg.classList.add('text-danger');
                }
                return;
            }

            const typesResult = await typesResponse.json();
            if (!typesResult.success) {
                dischargeDocList.innerHTML = '<p class="text-danger mb-0">Error loading patient document types.</p>';
                if (dischargeMsg) {
                    dischargeMsg.textContent = typesResult.message || 'Error loading patient document types.';
                    dischargeMsg.classList.remove('text-muted');
                    dischargeMsg.classList.add('text-danger');
                }
                return;
            }

            const docTypes = typesResult.data || [];
            if (docTypes.length === 0) {
                dischargeDocList.innerHTML = '<p class="text-muted mb-0">No patient document types found.</p>';
                if (dischargeMsg) {
                    dischargeMsg.textContent = 'No patient document types found for configuration.';
                    dischargeMsg.classList.remove('text-danger');
                    dischargeMsg.classList.add('text-muted');
                }
                return;
            }

            // 2) Existing settings for this discharge type
            const settingsResponse = await fetch('/Settings/GetDischargeDocumentSettings?dischargeTypeId=' + encodeURIComponent(dischargeTypeId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            let selectedIds = new Set();
            if (settingsResponse.ok) {
                const settingsResult = await settingsResponse.json();
                if (settingsResult.success) {
                    const existing = settingsResult.data || [];
                    existing.forEach(x => {
                        if (x.documentTypeId) {
                            selectedIds.add(x.documentTypeId.toString());
                        }
                    });
                }
            }

            // 3) Build checkbox list
            const wrapper = document.createElement('div');
            wrapper.className = 'list-group';

            docTypes.forEach(d => {
                const label = document.createElement('label');
                label.className = 'list-group-item d-flex align-items-center';

                const cb = document.createElement('input');
                cb.type = 'checkbox';
                cb.className = 'form-check-input me-2 discharge-doc-chk';
                cb.value = d.documentTypeId;

                if (selectedIds.has((d.documentTypeId || '').toString())) {
                    cb.checked = true;
                }

                const span = document.createElement('span');
                span.textContent = d.documentTypeName;

                label.appendChild(cb);
                label.appendChild(span);
                wrapper.appendChild(label);
            });

            dischargeDocList.innerHTML = '';

            const helper = document.createElement('p');
            helper.className = 'small text-muted mb-2';
            helper.textContent = 'Tick the documents that are mandatory for the selected discharge type.';

            dischargeDocList.appendChild(helper);
            dischargeDocList.appendChild(wrapper);

            if (dischargeMsg) {
                dischargeMsg.textContent = 'Configure which documents are mandatory for this discharge type.';
                dischargeMsg.classList.remove('text-danger');
                dischargeMsg.classList.add('text-muted');
            }
        } catch (err) {
            console.error('Error loading discharge document settings', err);
            dischargeDocList.innerHTML = '<p class="text-danger mb-0">Error loading document settings.</p>';
            if (dischargeMsg) {
                dischargeMsg.textContent = 'Error loading document settings.';
                dischargeMsg.classList.remove('text-muted');
                dischargeMsg.classList.add('text-danger');
            }
        }
    }

    function getSelectedDischargeDocumentTypeIds() {
        if (!dischargeDocList) return [];

        const checkboxes = dischargeDocList.querySelectorAll('input.discharge-doc-chk[type="checkbox"]');
        const ids = [];

        checkboxes.forEach(cb => {
            if (cb.checked && cb.value) {
                ids.push(cb.value);
            }
        });

        return ids;
    }

    async function saveDischargeSettings() {
        if (!selDischargeType) return;

        const dischargeTypeId = selDischargeType.value;

        if (!dischargeTypeId) {
            if (dischargeMsg) {
                dischargeMsg.textContent = 'Please select a discharge type before saving.';
                dischargeMsg.classList.remove('text-muted');
                dischargeMsg.classList.add('text-danger');
            }
            return;
        }

        const docIds = getSelectedDischargeDocumentTypeIds();

        const payload = {
            dischargeTypeId: dischargeTypeId,
            documentTypeIds: docIds
        };

        try {
            const response = await fetch('/Settings/SaveDischargeDocumentSettings', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                if (dischargeMsg) {
                    dischargeMsg.textContent = 'Server error while saving discharge settings.';
                    dischargeMsg.classList.remove('text-muted');
                    dischargeMsg.classList.add('text-danger');
                }
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (dischargeMsg) {
                    dischargeMsg.textContent = result.message || 'Failed to save discharge settings.';
                    dischargeMsg.classList.remove('text-muted');
                    dischargeMsg.classList.add('text-danger');
                }
                return;
            }

            if (dischargeMsg) {
                dischargeMsg.textContent = result.message || 'Discharge settings saved successfully.';
                dischargeMsg.classList.remove('text-danger');
                dischargeMsg.classList.add('text-success');
            }
        } catch (err) {
            console.error('Error saving discharge settings', err);
            if (dischargeMsg) {
                dischargeMsg.textContent = 'An unexpected error occurred while saving discharge settings.';
                dischargeMsg.classList.remove('text-muted');
                dischargeMsg.classList.add('text-danger');
            }
        }
    }

        document.addEventListener('DOMContentLoaded', function() {
            // Staff settings
            loadStaffTypes();

            if (selStaffType) {
                selStaffType.addEventListener('change', function() {
                    const staffTypeId = selStaffType.value;
                    loadStaffDocumentSettings(staffTypeId);
                });
            }

            if (btnSave) {
                btnSave.addEventListener('click', saveSettings);
            }

            // Discharge settings
            loadDischargeTypes();

            if (selDischargeType) {
                selDischargeType.addEventListener('change', function() {
                    const dischargeTypeId = selDischargeType.value;
                    loadDischargeDocumentSettings(dischargeTypeId);
                });
            }

            if (btnSaveDischarge) {
                btnSaveDischarge.addEventListener('click', saveDischargeSettings);
            }
        });
    })();