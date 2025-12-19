// @ts-nocheck
(function() {
    let staffLoaded = false;
    let staffCache = []; // keep full objects so we can auto-fill name

    function $(id) { return document.getElementById(id); }

    function setMessage(text, isSuccess) {
        const el = $('registerMessage');
        if (!el) return;
        el.className = isSuccess ? 'text-success' : 'text-danger';
        el.textContent = text || '';
    }

    function clearMessage() {
        const el = $('registerMessage');
        if (!el) return;
        el.className = '';
        el.textContent = '';
    }

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

            // nice label in UI
            const branch = (s.branchName || '').toString().trim();
            opt.textContent = branch ? `${name} (${branch})` : name;

            // store name so we can auto-fill
            opt.dataset.staffName = name;

            selectEl.appendChild(opt);
        });
    }

    async function loadStaffList() {
        if (staffLoaded) return;

        try {
            const res = await fetch('/Staff/GetStaffList', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!res.ok) throw new Error('HTTP ' + res.status);

            const list = await res.json();
            if (!Array.isArray(list)) throw new Error('Invalid staff list response');

            // normalize expected shape from your endpoint
            staffCache = list.map(x => ({
                staffId: x.staffId,
                name: x.name,
                branchName: x.branchName
            }));

            setStaffOptions($('StaffId'), staffCache);
            staffLoaded = true;
        } catch (err) {
            console.error('Error loading staff list', err);
            setStaffOptions($('StaffId'), []);
            staffLoaded = false;
            setMessage('Error loading staff list.', false);
        }
    }

    function setStaffMode(enabled) {
        const group = $('staffLinkGroup');
        const ddl = $('StaffId');
        const nameInput = $('Name');

        if (!group || !ddl || !nameInput) return;

        if (enabled) {
            group.classList.remove('d-none');

            // load staff only when STAFF is selected
            loadStaffList();

            // lock name when STAFF (we will auto-fill from selected staff)
            nameInput.readOnly = true;

            // if staff already selected, sync name
            const selectedOpt = ddl.options[ddl.selectedIndex];
            const staffName = selectedOpt?.dataset?.staffName || '';
            if (staffName) nameInput.value = staffName;
        } else {
            group.classList.add('d-none');

            // clear staff selection
            ddl.value = '';

            // unlock name for non-staff
            nameInput.readOnly = false;
        }
    }

    function onUserTypeChanged() {
        const userType = ($('UserType')?.value || '').toString().trim();
        setStaffMode(userType === '3');
    }

    function onStaffChanged() {
        const ddl = $('StaffId');
        const nameInput = $('Name');
        if (!ddl || !nameInput) return;

        const selectedOpt = ddl.options[ddl.selectedIndex];
        const staffName = selectedOpt?.dataset?.staffName || '';
        if (staffName) nameInput.value = staffName;
    }

    function buildPayload() {
        const name = ($('Name')?.value || '').trim();
        const username = ($('Username')?.value || '').trim();
        const email = ($('Email')?.value || '').trim();
        const password = ($('Password')?.value || '').trim();
        const userTypeStr = ($('UserType')?.value || '').trim();
        const userType = parseInt(userTypeStr || '3', 10);

        const isStaff = userTypeStr === '3';
        const staffId = isStaff ? (($('StaffId')?.value || '').trim()) : null;

        return {
            name,
            username,
            email,
            password,
            userType,
            staffId
        };
    }

    async function registerUser() {
        clearMessage();

        const payload = buildPayload();

        // Basic validation
        if (!payload.name || !payload.username || !payload.email || !payload.password) {
            setMessage('Please fill in all required fields.', false);
            return;
        }

        // STAFF requires StaffId
        if (payload.userType === 3 && (!payload.staffId || payload.staffId.length === 0)) {
            setMessage('Please select a Staff to link this user.', false);
            return;
        }

        try {
            const res = await fetch('/Account/RegisterUser', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
            });

            const result = await res.json();

            if (!res.ok) {
                setMessage(result?.message || 'Server error registering user.', false);
                return;
            }

            if (!result.success) {
                setMessage(result.message || 'Failed to register user.', false);
                return;
            }

            setMessage(result.message || 'User registered successfully.', true);

            // Optional: reset password field after success
            const pw = $('Password');
            if (pw) pw.value = '';
        } catch (err) {
            console.error('Register error', err);
            setMessage('An unexpected error occurred.', false);
        }
    }

    document.addEventListener('DOMContentLoaded', function() {
        const userTypeEl = $('UserType');
        const staffEl = $('StaffId');
        const btn = $('btnRegister');

        if (userTypeEl) userTypeEl.addEventListener('change', onUserTypeChanged);
        if (staffEl) staffEl.addEventListener('change', onStaffChanged);
        if (btn) btn.addEventListener('click', registerUser);

        // Initial state
        onUserTypeChanged();
    });
})();