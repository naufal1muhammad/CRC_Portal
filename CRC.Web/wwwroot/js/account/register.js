// @ts-nocheck
(function () {
    let staffLoaded = false;
    let staffCache = [];
    let passwordPolicy = null;

    let usersCache = [];

    let usersTableBody = null;
    let usersDataTable = null;

    let addUserModalEl = null;
    let addUserModal = null;

    const ENDPOINTS = {
        staffList: '/Staff/GetStaffList',
        registerUser: '/Account/RegisterUser',
        getUsers: '/Account/GetUsers',
        passwordPolicy: '/Account/GetPasswordPolicy'
    };

    function el(id) { return document.getElementById(id); }

    function showToast(text, ok) {
        if (!text) return;
        const T = window.Toastify;
        if (typeof T !== 'function') return;

        T({
            text,
            duration: 3500,
            gravity: 'top',
            position: 'right',
            close: true,
            style: {
                background: ok ? '#198754' : '#dc3545'
            }
        }).showToast();
    }

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
    // Display timestamps in Malaysian local time (Asia/Kuala_Lumpur) with AM/PM.
    // NOTE: Backend sends UTC ISO (with trailing "Z"). If we ever receive an ISO without timezone,
    // we treat it as UTC to avoid accidentally interpreting it as the browser's local time.
    function formatDateTime(iso) {
        if (!iso) return '';

        let s = (iso + '').trim();
        // If there's no timezone designator, assume UTC.
        if (/^\d{4}-\d{2}-\d{2}T/.test(s) && !(/[zZ]$/.test(s) || /[+\-]\d{2}:\d{2}$/.test(s))) {
            s += 'Z';
        }

        const d = new Date(s);
        if (isNaN(d.getTime())) return '';

        const parts = new Intl.DateTimeFormat('en-GB', {
            timeZone: 'Asia/Kuala_Lumpur',
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
            hour12: true
        }).formatToParts(d);

        const map = {};
        parts.forEach(p => { map[p.type] = p.value; });

        const dd = map.day || '';
        const mm = map.month || '';
        const yyyy = map.year || '';
        const hh = map.hour || '';
        const mi = map.minute || '';
        const ap = (map.dayPeriod || '').toUpperCase();

        return `${dd}/${mm}/${yyyy} ${hh}:${mi}${ap ? ' ' + ap : ''}`.trim();
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
    // Password policy
    // -----------------------------
    async function loadPasswordPolicy() {
        if (passwordPolicy) return;
        try {
            const res = await fetch(ENDPOINTS.passwordPolicy, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });
            if (res.ok) {
                passwordPolicy = await res.json();
            }
        } catch (err) {
            console.error('Error loading password policy', err);
        }
    }

    function validatePassword(password) {
        if (!passwordPolicy) return [];
        const errors = [];

        if (password.length < passwordPolicy.requiredLength)
            errors.push('Password must be at least ' + passwordPolicy.requiredLength + ' characters long.');
        if (passwordPolicy.requireUppercase && !/[A-Z]/.test(password))
            errors.push('Password must contain at least one uppercase letter (A-Z).');
        if (passwordPolicy.requireLowercase && !/[a-z]/.test(password))
            errors.push('Password must contain at least one lowercase letter (a-z).');
        if (passwordPolicy.requireDigit && !/[0-9]/.test(password))
            errors.push('Password must contain at least one digit (0-9).');
        if (passwordPolicy.requireNonAlphanumeric && !/[^a-zA-Z0-9]/.test(password))
            errors.push('Password must contain at least one special character (e.g., !@#$%^&*).');
        if (passwordPolicy.requiredUniqueChars > 0) {
            var unique = new Set(password).size;
            if (unique < passwordPolicy.requiredUniqueChars)
                errors.push('Password must contain at least ' + passwordPolicy.requiredUniqueChars + ' unique characters.');
        }
        return errors;
    }

    // -----------------------------
    // Users DataTable
    // -----------------------------
    function initUsersDataTable() {
        const jq = window.jQuery;

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
            pageLength: 10,
            order: [[0, 'desc']],
            searching: true,
            // Hide the built-in search box; we provide our own input in the card header.
            dom: "rt<'row mt-3 align-items-center'<'col-md-6'i><'col-md-6 text-end'p>>"
        });

        // Re-apply the current search text after (re)initializing the DataTable.
        const q = el('usersSearch')?.value || '';
        if (q) usersDataTable.search(q).draw();
    }

    function applyUsersSearch(query) {
        const q = (query || '').toString().toLowerCase().trim();

        // DataTables path
        if (usersDataTable && typeof usersDataTable.search === 'function') {
            usersDataTable.search(q).draw();
            return;
        }

        // Fallback (no DataTables)
        const rows = usersTableBody ? usersTableBody.querySelectorAll('tr') : [];
        rows.forEach(tr => {
            const text = (tr.textContent || '').toLowerCase();
            tr.style.display = !q || text.includes(q) ? '' : 'none';
        });
    }

    function bindUsersSearchInput() {
        const input = el('usersSearch');
        if (!input) return;
        input.addEventListener('input', function () {
            applyUsersSearch(input.value);
        });
    }

    // -----------------------------
    // STAFF dropdown
    // -----------------------------
    function getUsedStaffIdSet() {
        const set = new Set();
        (usersCache || []).forEach(u => {
            const sid = (u && u.staffId ? (u.staffId + '').trim() : '');
            if (sid) set.add(sid);
        });
        return set;
    }

    function setStaffOptions(selectEl, staffList, usedStaffIds) {
        if (!selectEl) return;

        selectEl.innerHTML = '';

        const ph = document.createElement('option');
        ph.value = '';
        ph.textContent = '-- Select Staff --';
        selectEl.appendChild(ph);

        const used = usedStaffIds instanceof Set ? usedStaffIds : new Set();

        (staffList || []).forEach(s => {
            const staffId = (s.staffId || '').toString().trim();
            const name = (s.name || '').toString().trim();
            if (!staffId || !name) return;

            const opt = document.createElement('option');
            opt.value = staffId;

            const branch = (s.branchName || '').toString().trim();
            const baseText = branch ? `${name} (${branch})` : name;

            // Prevent linking one Staff ID to multiple user accounts (UX improvement).
            if (used.has(staffId)) {
                opt.disabled = true;
                opt.textContent = baseText + ' (Already linked)';
            } else {
                opt.textContent = baseText;
            }

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

            setStaffOptions(el('StaffId'), staffCache, getUsedStaffIdSet());
            staffLoaded = true;
        } catch (err) {
            console.error('Error loading staff list', err);
            setStaffOptions(el('StaffId'), [], new Set());
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
            // (Re)load staff list if needed, and always re-apply "already linked" disabling
            // based on the latest users list.
            if (staffLoaded) {
                setStaffOptions(ddl, staffCache, getUsedStaffIdSet());
            } else {
                loadStaffList();
            }

            nameInput.readOnly = true;

            // Reset selections when switching to STAFF to avoid linking wrong staff.
            ddl.value = '';
            nameInput.value = '';

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

        const jq = window.jQuery;
        if (jq && jq.fn && jq.fn.dataTable && jq.fn.dataTable.isDataTable('#usersTable')) {
            jq('#usersTable').DataTable().destroy();
        }
        usersDataTable = null;
        usersCache = [];

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
            usersCache = Array.isArray(users) ? users : [];

            if (!Array.isArray(users) || users.length === 0) {
                usersCache = [];
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
    function resetRegisterForm() {
        clearMessage();

        const nameEl = el('Name');
        const userEl = el('Username');
        const emailEl = el('Email');
        const passEl = el('Password');
        const typeEl = el('UserType');
        const staffEl = el('StaffId');

        if (nameEl) nameEl.value = '';
        if (userEl) userEl.value = '';
        if (emailEl) emailEl.value = '';
        if (passEl) passEl.value = '';

        if (typeEl) typeEl.value = '2'; // default ADMIN

        if (staffEl) staffEl.value = '';

        // Ensure UI matches default type
        onUserTypeChanged();
    }

    function openAddUserModal() {
        if (!addUserModalEl || !addUserModal) return;
        resetRegisterForm();
        addUserModal.show();

        // focus first input
        setTimeout(() => {
            el('Username')?.focus();
        }, 150);
    }

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

        var pwErrors = validatePassword(payload.password);
        if (pwErrors.length > 0) {
            setMessage(pwErrors.join(' '), false);
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
                const m = result?.message || 'Server error registering user.';
                setMessage(m, false);
                showToast(m, false);
                return;
            }

            if (!result.success) {
                const m = result.message || 'Failed to register user.';
                setMessage(m, false);
                showToast(m, false);
                return;
            }

            const okMsg = result.message || 'User registered successfully.';
            setMessage(okMsg, true);
            showToast(okMsg, true);

            // Close modal on success and refresh table
            if (addUserModal) addUserModal.hide();

            const pw = el('Password');
            if (pw) pw.value = '';

            await loadUsers();
        } catch (err) {
            console.error('Register error', err);
            const m = err.message || 'An unexpected error occurred.';
            setMessage(m, false);
            showToast(m, false);
        }
    }

    // -----------------------------
    // Init
    // -----------------------------
    document.addEventListener('DOMContentLoaded', function () {
        usersTableBody = document.querySelector('#usersTable tbody');

        // Bootstrap modal instance
        addUserModalEl = el('addUserModal');
        if (addUserModalEl && window.bootstrap && typeof window.bootstrap.Modal === 'function') {
            addUserModal = new window.bootstrap.Modal(addUserModalEl);

            // Cleanup when closed
            addUserModalEl.addEventListener('hidden.bs.modal', () => {
                clearMessage();
            });
        }

        const userTypeEl = el('UserType');
        const staffEl = el('StaffId');
        const btnReg = el('btnRegister');
        const btnAdd = el('btnAddUser');
        const searchEl = el('usersSearch');

        if (userTypeEl) userTypeEl.addEventListener('change', onUserTypeChanged);
        if (staffEl) staffEl.addEventListener('change', onStaffChanged);
        if (btnReg) btnReg.addEventListener('click', registerUser);
        if (btnAdd) btnAdd.addEventListener('click', openAddUserModal);
        if (searchEl) searchEl.addEventListener('input', (e) => applyUsersSearch(e.target.value));

        loadPasswordPolicy();
        onUserTypeChanged();
        loadUsers();
    });
})();