// @ts-nocheck
(function () {
    let staffLoaded = false;
    let staffCache = [];

    let usersTableBody = null;
    let usersDataTable = null;

    const ENDPOINTS = {
        staffList: '/Staff/GetStaffList',
        registerUser: '/Account/RegisterUser',
        getUsers: '/Account/GetUsers'
    };

    // ✅ DO NOT name this "$" (it will shadow jQuery)
    function el(id) { return document.getElementById(id); }

    function setMessage(text, ok) {
        const msg = el('registerMessage');
        if (!msg) return;
        msg.className = ok ? 'text-success' : 'text-danger';
        msg.textContent = text || '';
    }

    function clearMessage() {
        const msg = el('registerMessage');
        if (!msg) return;
        msg.className = '';
        msg.textContent = '';
    }

    // -----------------------------
    // Helpers
    // -----------------------------
    function formatDateTime(iso) {
        if (!iso) return '';
        const d = new Date(iso);
        if (isNaN(d.getTime())) return '';

        const dd = String(d.getDate()).padStart(2, '0');
        const mm = String(d.getMonth() + 1).padStart(2, '0');
        const yyyy = d.getFullYear();
        const hh = String(d.getHours()).padStart(2, '0');
        const mi = String(d.getMinutes()).padStart(2, '0');

        return `${dd}/${mm}/${yyyy} ${hh}:${mi}`;
    }

    async function readJsonSafe(response) {
        const raw = await response.text();
        try {
            return JSON.parse(raw);
        } catch {
            throw new Error(raw.slice(0, 200) || 'Response is not valid JSON.');
        }
    }

    // -----------------------------
    // Users DataTable
    // -----------------------------
    function initUsersDataTable() {
        const jq = window.jQuery; // ✅ use real jQuery

        // If DataTables isn't loaded, stop (table will still show rows)
        if (!jq || !jq.fn || !jq.fn.dataTable) {
            console.warn('DataTables not found on this page (jQuery/DataTables not loaded).');
            return;
        }

        // Destroy existing instance if any
        if (jq.fn.dataTable.isDataTable('#usersTable')) {
            jq('#usersTable').DataTable().destroy();
        }

        usersDataTable = jq('#usersTable').DataTable({
            paging: true,
            lengthChange: true,
            pageLength: 10,
            order: [[0, 'desc']]
        });
    }

    // -----------------------------
    // STAFF dropdown
    // -----------------------------
    function setStaffOptions(selectEl, staffList) {
        if (!selectEl) return;

        selectEl.innerHTML = '';

        const ph = document.createElement('option');
        ph.value = '';
        ph.textContent = '-- Select Staff --';
        selectEl.appendChild(ph);

        (staffList || []).forEach(s => {
            const staffId = (s.staffId || '').toString().trim();
            const name = (s.name || '').toString().trim();
            if (!staffId || !name) return;

            const opt = document.createElement('option');
            opt.value = staffId;

            const branch = (s.branchName || '').toString().trim();
            opt.textContent = branch ? `${name} (${branch})` : name;

            opt.dataset.staffName = name;
            selectEl.appendChild(opt);
        });
    }

    async function loadStaffList() {
        if (staffLoaded) return;

        try {
            const res = await fetch(ENDPOINTS.staffList, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!res.ok) throw new Error('HTTP ' + res.status);

            const list = await res.json();
            if (!Array.isArray(list)) throw new Error('Invalid staff list response');

            staffCache = list.map(x => ({
                staffId: x.staffId,
                name: x.name,
                branchName: x.branchName
            }));

            setStaffOptions(el('StaffId'), staffCache);
            staffLoaded = true;
        } catch (err) {
            console.error('Error loading staff list', err);
            setStaffOptions(el('StaffId'), []);
            staffLoaded = false;
            setMessage('Error loading staff list.', false);
        }
    }

    function setStaffMode(enabled) {
        const group = el('staffLinkGroup');
        const ddl = el('StaffId');
        const nameInput = el('Name');

        if (!group || !ddl || !nameInput) return;

        if (enabled) {
            group.classList.remove('d-none');
            loadStaffList();
            nameInput.readOnly = true;

            const selectedOpt = ddl.options[ddl.selectedIndex];
            const staffName = selectedOpt?.dataset?.staffName || '';
            if (staffName) nameInput.value = staffName;
        } else {
            group.classList.add('d-none');
            ddl.value = '';
            nameInput.readOnly = false;
        }
    }

    function onUserTypeChanged() {
        const userType = ((el('UserType')?.value || '') + '').trim();
        setStaffMode(userType === '3');
    }

    function onStaffChanged() {
        const ddl = el('StaffId');
        const nameInput = el('Name');
        if (!ddl || !nameInput) return;

        const selectedOpt = ddl.options[ddl.selectedIndex];
        const staffName = selectedOpt?.dataset?.staffName || '';
        if (staffName) nameInput.value = staffName;
    }

    // -----------------------------
    // USERS list (manual fetch + tbody build)
    // -----------------------------
    async function loadUsers() {
        if (!usersTableBody) return;

        // ✅ IMPORTANT: destroy DataTables BEFORE touching tbody
        const jq = window.jQuery;
        if (jq && jq.fn && jq.fn.dataTable && jq.fn.dataTable.isDataTable('#usersTable')) {
            jq('#usersTable').DataTable().destroy();
        }
        usersDataTable = null;

        // show loading row
        usersTableBody.innerHTML = `
        <tr>
            <td colspan="8" class="text-center text-muted">Loading...</td>
        </tr>
    `;

        try {
            const response = await fetch(ENDPOINTS.getUsers, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                let msg = 'Error loading users.';
                try {
                    const errJson = await readJsonSafe(response);
                    msg = errJson?.message || msg;
                } catch (e) {
                    msg = e.message || msg;
                }

                usersTableBody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-danger">${msg}</td>
                </tr>
            `;

                initUsersDataTable();
                return;
            }

            const result = await readJsonSafe(response);

            if (!result || result.success !== true) {
                usersTableBody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-danger">
                        ${(result && result.message) ? result.message : 'Error loading users.'}
                    </td>
                </tr>
            `;
                initUsersDataTable();
                return;
            }

            const users = result.users || [];

            if (!Array.isArray(users) || users.length === 0) {
                usersTableBody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-muted">No users found.</td>
                </tr>
            `;
                initUsersDataTable();
                return;
            }

            // build rows
            usersTableBody.innerHTML = '';

            users.forEach(u => {
                const tr = document.createElement('tr');
                tr.innerHTML = `
                <td>${u.userId ?? ''}</td>
                <td>${u.name ?? ''}</td>
                <td>${u.username ?? ''}</td>
                <td>${u.email ?? ''}</td>
                <td>${u.userTypeName ?? ''}</td>
                <td>${u.staffId ?? ''}</td>
                <td>${formatDateTime(u.createdAt)}</td>
                <td>${formatDateTime(u.lastLogin)}</td>
            `;
                usersTableBody.appendChild(tr);
            });

            // ✅ re-init AFTER rows are in DOM
            initUsersDataTable();
        } catch (err) {
            console.error('Error loading users', err);

            usersTableBody.innerHTML = `
            <tr>
                <td colspan="8" class="text-center text-danger">Error loading users.</td>
            </tr>
        `;

            setMessage(err.message || 'Error loading users.', false);
            initUsersDataTable();
        }
    }

    // -----------------------------
    // Register
    // -----------------------------
    function buildPayload() {
        const name = ((el('Name')?.value || '') + '').trim();
        const username = ((el('Username')?.value || '') + '').trim();
        const email = ((el('Email')?.value || '') + '').trim();
        const password = ((el('Password')?.value || '') + '').trim();

        const userTypeStr = ((el('UserType')?.value || '') + '').trim();
        const userType = parseInt(userTypeStr || '3', 10);

        const isStaff = userTypeStr === '3';
        const staffId = isStaff ? (((el('StaffId')?.value || '') + '').trim()) : null;

        return { name, username, email, password, userType, staffId };
    }

    async function registerUser() {
        clearMessage();

        const payload = buildPayload();

        if (!payload.name || !payload.username || !payload.email || !payload.password) {
            setMessage('Please fill in all required fields.', false);
            return;
        }

        if (payload.userType === 3 && (!payload.staffId || payload.staffId.length === 0)) {
            setMessage('Please select a Staff to link this user.', false);
            return;
        }

        try {
            const res = await fetch(ENDPOINTS.registerUser, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            const result = await readJsonSafe(res);

            if (!res.ok) {
                setMessage(result?.message || 'Server error registering user.', false);
                return;
            }

            if (!result.success) {
                setMessage(result.message || 'Failed to register user.', false);
                return;
            }

            setMessage(result.message || 'User registered successfully.', true);

            const pw = el('Password');
            if (pw) pw.value = '';

            await loadUsers();
        } catch (err) {
            console.error('Register error', err);
            setMessage(err.message || 'An unexpected error occurred.', false);
        }
    }

    // -----------------------------
    // Init
    // -----------------------------
    document.addEventListener('DOMContentLoaded', function () {
        usersTableBody = document.querySelector('#usersTable tbody');

        const userTypeEl = el('UserType');
        const staffEl = el('StaffId');
        const btnReg = el('btnRegister');

        if (userTypeEl) userTypeEl.addEventListener('change', onUserTypeChanged);
        if (staffEl) staffEl.addEventListener('change', onStaffChanged);
        if (btnReg) btnReg.addEventListener('click', registerUser);

        onUserTypeChanged();
        loadUsers();
    });
})();