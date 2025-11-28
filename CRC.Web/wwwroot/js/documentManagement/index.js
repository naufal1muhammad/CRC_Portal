// @ts-nocheck
(function() {
    const patientDocTableBody = document.querySelector('#patientDocTable tbody');
    const staffDocTableBody = document.querySelector('#staffDocTable tbody');
    const txtSearch = document.getElementById('documentSearch');

    function getActiveDocTab() {
        const activeTab = document.querySelector('#docMgmtTabs .nav-link.active');
        if (!activeTab) return 'patient';
        return activeTab.id === 'staff-doc-tab' ? 'staff' : 'patient';
    }

        function applyDocumentSearchFilter() {
        if (!txtSearch) return;

        const filter = txtSearch.value.trim().toLowerCase();
        const active = getActiveDocTab();

        if (active === 'patient') {
            if (!patientDocTableBody) return;
            const rows = patientDocTableBody.querySelectorAll('tr');

            rows.forEach(row => {
                const cells = row.querySelectorAll('td');
                if (cells.length < 2) return;

                const idText = (cells[0].textContent || '').toLowerCase();
                const nameText = (cells[1].textContent || '').toLowerCase();
                const combined = idText + ' ' + nameText;

                if (!filter || combined.includes(filter)) {
                    row.style.display = '';
                } else {
                    row.style.display = 'none';
                }
            });
        } else if (active === 'staff') {
            if (!staffDocTableBody) return;
            const rows = staffDocTableBody.querySelectorAll('tr');

            rows.forEach(row => {
                const cells = row.querySelectorAll('td');
                if (cells.length < 2) return;

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
    }

    async function loadPatientDocuments() {
        if (!patientDocTableBody) return;

        patientDocTableBody.innerHTML = '<tr><td colspan="4">Loading...</td></tr>';

        try {
            const response = await fetch('/DocumentManagement/GetPatientDocuments', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                patientDocTableBody.innerHTML = '<tr><td colspan="4">Error loading patient documents.</td></tr>';
                return;
            }

            const docs = await response.json(); // array

            if (!docs || docs.length === 0) {
                patientDocTableBody.innerHTML = '<tr><td colspan="4" class="text-muted">No patient documents found.</td></tr>';
                return;
            }

            patientDocTableBody.innerHTML = '';

            docs.forEach(d => {
                const tr = document.createElement('tr');

                const safeFileName = d.fileName || '';
                const safeFilePath = d.filePath || '#';

                tr.innerHTML = `
                    <td>${d.patientId || ''}</td>
                    <td>${d.patientName || ''}</td>
                    <td>
                        <a href="${safeFilePath}"
                           target="_blank"
                           rel="noopener noreferrer"
                           class="text-decoration-none">
                            ${safeFileName}
                        </a>
                    </td>
                    <td>${d.uploadedOn || ''}</td>
                `;

                patientDocTableBody.appendChild(tr);
            });
            applyDocumentSearchFilter();
        } catch (err) {
            console.error('Error loading patient documents', err);
            patientDocTableBody.innerHTML = '<tr><td colspan="4">Error loading patient documents.</td></tr>';
        }
    }

    async function loadStaffDocuments() {
        if (!staffDocTableBody) return;

        staffDocTableBody.innerHTML = '<tr><td colspan="4">Loading...</td></tr>';

        try {
            const response = await fetch('/DocumentManagement/GetStaffDocuments', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                staffDocTableBody.innerHTML = '<tr><td colspan="4">Error loading staff documents.</td></tr>';
                return;
            }

            const docs = await response.json(); // array

            if (!docs || docs.length === 0) {
                staffDocTableBody.innerHTML = '<tr><td colspan="4" class="text-muted">No staff documents found.</td></tr>';
                return;
            }

            staffDocTableBody.innerHTML = '';

            docs.forEach(d => {
                const tr = document.createElement('tr');

                const safeFileName = d.fileName || '';
                const safeFilePath = d.filePath || '#';

                tr.innerHTML = `
                    <td>${d.staffId || ''}</td>
                    <td>${d.staffName || ''}</td>
                    <td>
                        <a href="${safeFilePath}"
                           target="_blank"
                           rel="noopener noreferrer"
                           class="text-decoration-none">
                            ${safeFileName}
                        </a>
                    </td>
                    <td>${d.uploadedOn || ''}</td>
                `;

                staffDocTableBody.appendChild(tr);
            });
            applyDocumentSearchFilter();
        } catch (err) {
            console.error('Error loading staff documents', err);
            staffDocTableBody.innerHTML = '<tr><td colspan="4">Error loading staff documents.</td></tr>';
        }
    }

    document.addEventListener('DOMContentLoaded', function() {
        // Load both on page load
        loadPatientDocuments();
        loadStaffDocuments();

        if (txtSearch) {
            txtSearch.addEventListener('input', function () {
                applyDocumentSearchFilter();
            });
        }
    });
})();