// @ts-nocheck
(function() {
    const selStaffType = document.getElementById('SettingsStaffType');
    const docList = document.getElementById('documentTypeList');
    const msg = document.getElementById('settingsMessage');
    const btnSave = document.getElementById('btnSaveSettings');

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

    document.addEventListener('DOMContentLoaded', function() {
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
    });
})();