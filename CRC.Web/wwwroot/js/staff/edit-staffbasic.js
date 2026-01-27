// @ts-nocheck
(function() {
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
    }

    function selectOptionByText(select, text) {
        if (!select || !text) return;
        const target = text.trim().toLowerCase();

        for (let i = 0; i < select.options.length; i++) {
            const opt = select.options[i];
            if ((opt.textContent || '').trim().toLowerCase() === target) {
                select.value = opt.value;
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

        citySelect.disabled = true;
        setSelectOptions(citySelect, [], 'id', 'name', '-- Select City --');
        if (postcodeSelect) {
            postcodeSelect.disabled = true;
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
            citySelect.disabled = false;
        } catch (err) {
            console.error(err);
        }
    }

    async function loadPostcodesByCity(cityId) {
        const postcodeSelect = document.getElementById('StaffResPostcode');
        if (!postcodeSelect) return;

        postcodeSelect.disabled = true;
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
            postcodeSelect.disabled = false;
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

            if (headerName) headerName.textContent = 'Staff: ' + (s.name || '-');
            if (headerId && s.staffId) headerId.textContent = 'ID: ' + s.staffId;
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'Error loading staff details.';
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
            const response = await fetch('/Staff/SaveStaff', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify(payload)
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
                return;
            }

            if (msg) {
                msg.textContent = result.message || 'Staff saved successfully.';
                msg.classList.add('text-success');
            }

            if (window.StaffDocumentsTab && typeof window.StaffDocumentsTab.reload === 'function') {
                window.StaffDocumentsTab.reload();
            }
        } catch (err) {
            console.error(err);
            if (msg) {
                msg.textContent = 'An unexpected error occurred.';
                msg.classList.add('text-danger');
            }
        }
    }

    document.addEventListener('DOMContentLoaded', async function() {
        await loadLookups();

        const staffId = getStaffId();
        if (staffId) {
            setStaffId(staffId);
        }

        await loadStaffBasic(staffId);

        const nricInput = document.getElementById('StaffNRIC');
        if (nricInput) {
            nricInput.addEventListener('blur', updateBirthDateFromNric);
        }

        const stateSelect = document.getElementById('StaffResState');
        if (stateSelect) {
            stateSelect.addEventListener('change', async function() {
                await loadCitiesByState(stateSelect.value);
            });
        }

        const citySelect = document.getElementById('StaffResCity');
        if (citySelect) {
            citySelect.addEventListener('change', async function() {
                await loadPostcodesByCity(citySelect.value);
            });
        }

        const postcodeSelect = document.getElementById('StaffResPostcode');
        if (postcodeSelect) {
            postcodeSelect.addEventListener('change', updateAddressLineInputsEnabled);
        }

        const btnSave = document.getElementById('btnSaveStaffMain');
        if (btnSave) {
            btnSave.addEventListener('click', saveStaff);
        }
    });
})();
