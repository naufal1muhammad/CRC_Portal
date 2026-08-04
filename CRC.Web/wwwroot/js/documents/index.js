// @ts-nocheck
(function () {
    let modePatientRadio;
    let modeStaffRadio;
    let selName;
    let selDocType;
    let btnSearch;
    let btnClear;
    let tableBody;
    let msg;

    // Lookups cache
    let patientNames = [];
    let staffNames = [];
    let patientDocTypes = [];
    let staffDocTypes = [];

    // DataTables instance
    let documentsDataTable = null;

    function getCurrentMode() {
        const patientChecked = modePatientRadio && modePatientRadio.checked;
        return patientChecked ? 'Patient' : 'Staff';
    }

    function setSelectOptions(select, items, placeholderText) {
        if (!select) return;

        select.innerHTML = '';

        const optAll = document.createElement('option');
        optAll.value = '';
        optAll.textContent = placeholderText || '-- All --';
        select.appendChild(optAll);

        (items || []).forEach(item => {
            const value = (item && (item.id || item.value || item.name) || '').toString();
            const label = (item && (item.name || item.label || item.id) || '').toString();
            if (!value && !label) return;

            const opt = document.createElement('option');
            // Prefer stable IDs for filtering when provided
            opt.value = value || label;
            opt.textContent = label || value;
            select.appendChild(opt);
        });
    }

    function applySelect2() {
        // Optional: only if jQuery + select2 are available
        if (window.$ && $.fn.select2) {
            $('#docIndividualName').select2({
                theme: 'bootstrap-5',
                width: '100%',
                placeholder: '-- All --',
                allowClear: true
            });

            $('#docDocumentType').select2({
                theme: 'bootstrap-5',
                width: '100%',
                placeholder: '-- All --',
                allowClear: true
            });
        }
    }

    function refreshFiltersForMode() {
        const mode = getCurrentMode();

        // If select2 is applied, destroy first so that new <option> items are reflected correctly.
        if (window.$ && $.fn.select2) {
            try {
                $('#docIndividualName').select2('destroy');
                $('#docDocumentType').select2('destroy');
            } catch (e) {
                // ignore if not initialised
            }
        }

        if (mode === 'Patient') {
            setSelectOptions(selName, patientNames, '-- All Patients --');
            setSelectOptions(selDocType, patientDocTypes, '-- All Doc Types --');
        } else {
            setSelectOptions(selName, staffNames, '-- All Staff --');
            setSelectOptions(selDocType, staffDocTypes, '-- All Doc Types --');
        }


        if (selName) selName.value = '';
        if (selDocType) selDocType.value = '';

        // Re-apply select2 (if available) after repopulating options
        applySelect2();
    }

    async function loadLookups() {
        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-success', 'text-danger');
        }

        try {
            const response = await fetch('/Documents/GetLookups', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                if (msg) {
                    msg.textContent = 'Error loading document lookups.';
                    msg.classList.add('text-danger');
                }
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (msg) {
                    msg.textContent = result.message || 'Error loading document lookups.';
                    msg.classList.add('text-danger');
                }
                return;
            }

            patientNames = result.patientNames || [];
            patientDocTypes = result.patientDocTypes || [];
            staffNames = result.staffNames || [];
            staffDocTypes = result.staffDocTypes || [];

            refreshFiltersForMode();
            applySelect2();
        } catch (err) {
            console.error('Error loading document lookups', err);
            if (msg) {
                msg.textContent = 'Error loading document lookups.';
                msg.classList.add('text-danger');
            }
        }
    }

    function destroyDataTableIfAny() {
        if (documentsDataTable && window.$ && $.fn.DataTable) {
            documentsDataTable.destroy();
            documentsDataTable = null;
        }
    }

    function initDataTable() {
        if (!window.$ || !$.fn.DataTable) return;

        destroyDataTableIfAny();

        documentsDataTable = $('#documentsTable').DataTable({
            paging: true,
            searching: true,   // internal DataTables search box
            lengthChange: true,
            pageLength: 10,
            order: [],         // no initial ordering
            language: {
                search: "Search:",
                lengthMenu: "Show _MENU_ records",
                info: "Showing _START_ to _END_ of _TOTAL_ records"
            }
        });
    }

    function clearFilters() {
        if (modePatientRadio) modePatientRadio.checked = true;
        if (modeStaffRadio) modeStaffRadio.checked = false;

        refreshFiltersForMode();

        if (tableBody) {
            tableBody.innerHTML = `
                <tr>
                    <td colspan="5" class="text-center text-muted">
                        Use the filters above and click <strong>Search</strong> to view documents.
                    </td>
                </tr>
            `;
        }

        destroyDataTableIfAny();

        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-danger', 'text-success');
        }
    }

    function renderResults(data) {
        if (!tableBody) return;

        destroyDataTableIfAny();

        if (!data || data.length === 0) {
            tableBody.innerHTML = `
                <tr>
                    <td colspan="5" class="text-center text-muted">
                        No documents found for the selected filters.
                    </td>
                </tr>
            `;
            return;
        }

        tableBody.innerHTML = '';

        // The mode is read once, here, and baked into every row. renderResults always runs straight after
        // the search that produced these rows, so this is the mode they were fetched with — and freezing it
        // means flipping the Patient/Staff radio afterwards cannot send a click to the wrong table.
        const mode = getCurrentMode();

        data.forEach(row => {
            const tr = document.createElement('tr');

            const id = row.id || '';
            const name = row.name || '';
            const docType = row.documentType || '';
            const fileName = row.fileName || '';
            const documentId = row.documentId || 0;
            const uploadedOn = row.uploadedOn || '';

            // The link carries only a document id and a mode. It is resolved at click time to a 5-minute
            // read SAS URL minted by the server, so no durable file URL is ever placed in the DOM: the
            // container is private and there is nothing in the page for anyone to copy, bookmark or share.
            tr.innerHTML = `
                <td>${id}</td>
                <td>${name}</td>
                <td>${docType}</td>
                <td>
                    <a href="#"
                       class="doc-open"
                       data-id="${documentId}"
                       data-mode="${mode}">
                        ${fileName}
                    </a>
                </td>
                <td>${uploadedOn}</td>
            `;

            tableBody.appendChild(tr);
        });

        // now turn the table into a DataTable
        initDataTable();
    }

    async function openDocument(mode, docId) {
        if (!mode || !docId || docId <= 0) return;

        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-danger', 'text-success');
        }

        try {
            const response = await fetch(
                '/Documents/DocumentUrl?mode=' + encodeURIComponent(mode) + '&id=' + encodeURIComponent(docId),
                {
                    method: 'GET',
                    headers: { 'Accept': 'application/json' }
                });

            if (!response.ok) {
                if (msg) {
                    msg.textContent = 'Server error while opening document.';
                    msg.classList.add('text-danger');
                }
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (msg) {
                    msg.textContent = result.message || 'Failed to open document.';
                    msg.classList.add('text-danger');
                }
                return;
            }

            window.open(result.url, '_blank', 'noopener,noreferrer');
        } catch (err) {
            console.error('Error opening document', err);
            if (msg) {
                msg.textContent = 'An unexpected error occurred while opening document.';
                msg.classList.add('text-danger');
            }
        }
    }

    async function performSearch() {
        const mode = getCurrentMode();

        const nameVal = selName ? (selName.value || '').trim() : '';
        const docTypeVal = selDocType ? (selDocType.value || '').trim() : '';

        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-danger', 'text-success');
        }

        if (tableBody) {
            destroyDataTableIfAny();
            tableBody.innerHTML = `
                <tr>
                    <td colspan="5" class="text-center text-muted">
                        Searching...
                    </td>
                </tr>
            `;
        }

        const payload = {
            mode: mode,                      // "Patient" or "Staff"
            individualName: nameVal || null,
            documentType: docTypeVal || null
        };

        try {
            const response = await fetch('/Documents/Search', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                    'X-CSRF-Token': $('input:hidden[name="__RequestVerificationToken"]').val()
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                if (msg) {
                    msg.textContent = 'Server error while searching documents.';
                    msg.classList.add('text-danger');
                }
                if (tableBody) {
                    tableBody.innerHTML = `
                        <tr>
                            <td colspan="5" class="text-center text-danger">
                                Server error while searching documents.
                            </td>
                        </tr>
                    `;
                }
                return;
            }

            const result = await response.json();

            if (!result.success) {
                if (msg) {
                    msg.textContent = result.message || 'Failed to search documents.';
                    msg.classList.add('text-danger');
                }
                if (tableBody) {
                    tableBody.innerHTML = `
                        <tr>
                            <td colspan="5" class="text-center text-danger">
                                ${result.message || 'Failed to search documents.'}
                            </td>
                        </tr>
                    `;
                }
                return;
            }

            renderResults(result.data || []);
        } catch (err) {
            console.error('Error searching documents', err);
            if (msg) {
                msg.textContent = 'An unexpected error occurred while searching documents.';
                msg.classList.add('text-danger');
            }
            if (tableBody) {
                tableBody.innerHTML = `
                    <tr>
                        <td colspan="5" class="text-center text-danger">
                            An unexpected error occurred while searching documents.
                        </td>
                    </tr>
                `;
            }
        }
    }

    function wireEvents() {
        if (modePatientRadio) {
            modePatientRadio.addEventListener('change', function () {
                if (modePatientRadio.checked) {
                    refreshFiltersForMode();
                }
            });
        }

        if (modeStaffRadio) {
            modeStaffRadio.addEventListener('change', function () {
                if (modeStaffRadio.checked) {
                    refreshFiltersForMode();
                }
            });
        }

        if (btnSearch) {
            btnSearch.addEventListener('click', function () {
                performSearch();
            });
        }

        if (btnClear) {
            btnClear.addEventListener('click', function () {
                clearFilters();
            });
        }

        // Delegated, and deliberately bound to `document`: the rows this handler serves are rebuilt by
        // renderResults and then again by DataTables on every paging, sorting and search interaction, so a
        // handler bound per row would be gone after the first page change. `document` outlives all of it.
        document.addEventListener('click', function (e) {
            const openLink = e.target.closest('.doc-open');
            if (!openLink) return;

            e.preventDefault();

            const idStr = openLink.getAttribute('data-id');
            const docId = idStr ? parseInt(idStr, 10) : 0;
            const mode = openLink.getAttribute('data-mode') || '';

            if (docId > 0) {
                openDocument(mode, docId);
            }
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        modePatientRadio = document.getElementById('docModePatient');
        modeStaffRadio = document.getElementById('docModeStaff');
        selName = document.getElementById('docIndividualName');
        selDocType = document.getElementById('docDocumentType');
        btnSearch = document.getElementById('btnDocSearch');
        btnClear = document.getElementById('btnDocClear');
        tableBody = document.querySelector('#documentsTable tbody');
        msg = document.getElementById('documentsMessage');

        wireEvents();
        loadLookups();
    });
})();