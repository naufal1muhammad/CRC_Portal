// @ts-nocheck
(function() {
    let lookupsLoaded = false;

    function getRootPatientId() {
        const root = document.querySelector('[data-patient-id]');
        return root ? (root.getAttribute('data-patient-id') || '') : '';
    }

    function setRootPatientId(newId) {
        const root = document.querySelector('[data-patient-id]');
        if (root && newId) {
            root.setAttribute('data-patient-id', newId);
        }
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

    function computeAgeFromBirthDateInput() {
        const birthInput = document.getElementById('PatientBirthDate');
        const ageInput = document.getElementById('PatientAge');
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

    async function loadLookups() {
        const msg = document.getElementById('basicDetailsMessage');

        try {
            const response = await fetch('/Patient/GetBasicLookups', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                if (msg) msg.textContent = 'Error loading lookups.';
                return;
            }

            const result = await response.json();
            if (!result.success) {
                if (msg) msg.textContent = result.message || 'Error loading lookups.';
                return;
            }

            const races = result.races || [];
            const sources = result.sources || [];
            const religions = result.religions || [];
            const maritalStatuses = result.maritalStatuses || [];
            const occupations = result.occupations || [];
            const branches = result.branches || [];

            setSelectOptions(document.getElementById('PatientRace'), races, 'id', 'name', '-- Select Race --');
            setSelectOptions(document.getElementById('PatientSource'), sources, 'id', 'name', '-- Select Source --');
            setSelectOptions(document.getElementById('PatientReligion'), religions, 'id', 'name', '-- Select Religion --');
            setSelectOptions(document.getElementById('PatientMaritalStatus'), maritalStatuses, 'id', 'name', '-- Select Marital Status --');
            setSelectOptions(document.getElementById('PatientOccupation'), occupations, 'id', 'name', '-- Select Occupation --');
            setSelectOptions(document.getElementById('PatientBranch'), branches, 'branchId', 'branchName', '-- Select Branch --');

            lookupsLoaded = true;
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'Error loading lookups.';
        }
    }

    function initDischargeFromBasic(patient) {
    if (!window.PatientDischargeTab || typeof window.PatientDischargeTab.init !== 'function') {
        return;
    }

    // New patient, or no discharge info yet
    if (!patient) {
        window.PatientDischargeTab.init(null);  // will just load dropdown + keep fields disabled
        return;
    }

    window.PatientDischargeTab.init({
        isDischarged: !!patient.dischargeTypeName,
        dischargeTypeId: null, // we only store name in PatientBasic; ID can be wired later if needed
        dischargeTypeName: patient.dischargeTypeName || '',
        dischargeDate: patient.dischargeDate || '',
        dischargeRemarks: patient.dischargeRemarks || ''
    });
}
    
    async function loadPatientBasic(patientId) {
        const msg = document.getElementById('basicDetailsMessage');
        const headerName = document.getElementById('patientHeaderName');
        const headerId = document.getElementById('patientHeaderId');
        const hidId = document.getElementById('PatientIdHidden');

        if (!patientId) {
    if (headerName) headerName.textContent = 'Patient: -';

    // 🔹 ensure Discharge tab still loads its dropdown for a brand-new patient
    initDischargeFromBasic(null);

    return;
}

        try {
            const response = await fetch('/Patient/GetBasic?patientId=' + encodeURIComponent(patientId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                if (msg) msg.textContent = 'Error loading patient details.';
                return;
            }

            const result = await response.json();
            if (!result.success) {
                if (msg) msg.textContent = result.message || 'Error loading patient details.';
                return;
            }

            const p = result.patient;
            if (!p) return;

            const txtName = document.getElementById('PatientName');
            const txtEmail = document.getElementById('PatientEmail');
            const txtPhone = document.getElementById('PatientPhone');
            const txtNRIC = document.getElementById('PatientNRIC');
            const txtAdmittedOn = document.getElementById('PatientAdmittedOn');
            const txtBirthDate = document.getElementById('PatientBirthDate');
            const selGender = document.getElementById('PatientGender');
            const selRace = document.getElementById('PatientRace');
            const selSource = document.getElementById('PatientSource');
            const selBranch = document.getElementById('PatientBranch');
            const selReligion = document.getElementById('PatientReligion');
            const selMarital = document.getElementById('PatientMaritalStatus');
            const txtAddress = document.getElementById('PatientAddress');
            const txtEmerName = document.getElementById('PatientEmergencyName');
            const txtEmerRel = document.getElementById('PatientEmergencyRelationship');
            const txtEmerNum = document.getElementById('PatientEmergencyNumber');
            const selOccupation = document.getElementById('PatientOccupation');

            if (txtName) txtName.value = p.name || '';
            if (txtEmail) txtEmail.value = p.email || '';
            if (txtPhone) txtPhone.value = p.phone || '';
            if (txtNRIC) txtNRIC.value = p.nric || '';
            if (txtAdmittedOn) txtAdmittedOn.value = p.admittedOn || '';
            if (txtBirthDate) txtBirthDate.value = p.birthDate || '';

            if (selGender) selGender.value = p.gender || '';

            selectOptionByText(selRace, p.raceName || '');
            selectOptionByText(selSource, p.sourceName || '');
            selectOptionByText(selBranch, p.branchName || '');
            selectOptionByText(selReligion, p.religionName || '');
            selectOptionByText(selMarital, p.maritalStatusName || '');
            selectOptionByText(selOccupation, p.occupationName || '');

            if (txtAddress) txtAddress.value = p.address || '';
            if (txtEmerName) txtEmerName.value = p.emergencyName || '';
            if (txtEmerRel) txtEmerRel.value = p.emergencyRelationship || '';
            if (txtEmerNum) txtEmerNum.value = p.emergencyNumber || '';

            if (hidId) hidId.value = p.patientId || '';

            if (headerName) headerName.textContent = 'Patient: ' + (p.name || '-');
            if (headerId && p.patientId) headerId.textContent = 'ID: ' + p.patientId;

            computeAgeFromBirthDateInput();
            initDischargeFromBasic(p);
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'Error loading patient details.';
        }
    }

    function collectBasicForm() {
        const hidId = document.getElementById('PatientIdHidden');

        const txtName = document.getElementById('PatientName');
        const txtEmail = document.getElementById('PatientEmail');
        const txtPhone = document.getElementById('PatientPhone');
        const txtNRIC = document.getElementById('PatientNRIC');
        const txtAdmittedOn = document.getElementById('PatientAdmittedOn');
        const txtBirthDate = document.getElementById('PatientBirthDate');
        const selGender = document.getElementById('PatientGender');
        const selRace = document.getElementById('PatientRace');
        const selSource = document.getElementById('PatientSource');
        const selBranch = document.getElementById('PatientBranch');
        const selReligion = document.getElementById('PatientReligion');
        const selMarital = document.getElementById('PatientMaritalStatus');
        const txtAddress = document.getElementById('PatientAddress');
        const txtEmerName = document.getElementById('PatientEmergencyName');
        const txtEmerRel = document.getElementById('PatientEmergencyRelationship');
        const txtEmerNum = document.getElementById('PatientEmergencyNumber');
        const selOccupation = document.getElementById('PatientOccupation');

        const getSelectedText = (sel) => {
            if (!sel || sel.selectedIndex < 0) return '';
            return sel.options[sel.selectedIndex].text || '';
        };

        return {
            patientId: hidId ? hidId.value.trim() : '',
            name: txtName ? txtName.value.trim() : '',
            email: txtEmail ? txtEmail.value.trim() : '',
            phone: txtPhone ? txtPhone.value.trim() : '',
            nric: txtNRIC ? txtNRIC.value.trim() : '',
            admittedOn: txtAdmittedOn ? txtAdmittedOn.value : '',
            birthDate: txtBirthDate ? txtBirthDate.value : '',
            raceName: getSelectedText(selRace),
            branchName: getSelectedText(selBranch),
            sourceName: getSelectedText(selSource),
            gender: selGender ? selGender.value : '',
            religionName: getSelectedText(selReligion),
            maritalStatusName: getSelectedText(selMarital),
            address: txtAddress ? txtAddress.value.trim() : '',
            emergencyName: txtEmerName ? txtEmerName.value.trim() : '',
            emergencyRelationship: txtEmerRel ? txtEmerRel.value.trim() : '',
            emergencyNumber: txtEmerNum ? txtEmerNum.value.trim() : '',
            occupationName: getSelectedText(selOccupation)
        };
    }

    function validateBasicForm(data) {
        if (!data.name ||
            !data.email ||
            !data.phone ||
            !data.nric ||
            !data.admittedOn ||
            !data.birthDate ||
            !data.raceName ||
            !data.branchName ||
            !data.sourceName ||
            !data.gender ||
            !data.religionName ||
            !data.maritalStatusName ||
            !data.address ||
            !data.emergencyName ||
            !data.emergencyRelationship ||
            !data.emergencyNumber ||
            !data.occupationName) {
            return 'Please fill in all mandatory fields.';
        }
        return '';
    }

async function saveBasic() {
    const msg = document.getElementById('basicDetailsMessage');
    if (msg) {
        msg.textContent = '';
        msg.classList.remove('text-success');
        msg.classList.add('text-danger');
    }

    // 1) Basic details as before
    const basicData = collectBasicForm();
    const error = validateBasicForm(basicData);
    if (error) {
        if (msg) msg.textContent = error;
        return;
    }

    // 2) Discharge info from Discharge tab (optional)
    let discharge = {
        isDischarged: false,
        dischargeTypeId: null,
        dischargeTypeName: null,
        dischargeDate: null,
        dischargeRemarks: null
    };

    if (window.PatientDischargeTab && typeof window.PatientDischargeTab.getPayload === 'function') {
        const d = window.PatientDischargeTab.getPayload();
        if (d === null) {
            // getPayload() already showed an error in the Discharge tab
            if (msg) {
                msg.textContent = 'Please fix the errors in the Discharge tab before saving.';
            }
            return;
        }
        discharge = d;
    }

    // 3) Combine basic + discharge into one payload
    const dataToSend = {
        ...basicData,
        isDischarged: discharge.isDischarged,
        dischargeTypeId: discharge.dischargeTypeId,
        dischargeTypeName: discharge.dischargeTypeName,
        dischargeDate: discharge.dischargeDate,        // yyyy-MM-dd from <input type="date">
        dischargeRemarks: discharge.dischargeRemarks
    };

    try {
        const response = await fetch('/Patient/SaveBasic', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify(dataToSend)
        });

        if (!response.ok) {
            if (msg) msg.textContent = 'Server error while saving patient details.';
            return;
        }

        const result = await response.json();

        if (!result.success) {
            if (msg) msg.textContent = result.message || 'Failed to save patient details.';
            return;
        }

        const newId = result.patientId || basicData.patientId;

// Update hidden + root data attribute
const hidId = document.getElementById('PatientIdHidden');
if (hidId && newId) hidId.value = newId;
setRootPatientId(newId);

// Update header (name + ID)
const headerName = document.getElementById('patientHeaderName');
const headerId = document.getElementById('patientHeaderId');
if (headerId && newId) headerId.textContent = 'ID: ' + newId;
if (headerName && basicData.name) headerName.textContent = 'Patient: ' + basicData.name;

// We no longer show text success here
if (msg) {
    msg.textContent = '';
    msg.classList.remove('text-success');
    msg.classList.add('text-danger'); // keep as "error style" by default
}

// 🔹 Wake up other tabs immediately (no need to leave and re-open)

// Enable Add Appointment button
const btnAddAppointment = document.getElementById('btnAddAppointment');
if (btnAddAppointment) {
    btnAddAppointment.disabled = false;
}

// Enable Add Journey button
const btnAddJourney = document.getElementById('btnAddJourney');
if (btnAddJourney) {
    btnAddJourney.disabled = false;
}
if (window.PatientDocumentsTab &&
    typeof window.PatientDocumentsTab.reload === 'function') {
    window.PatientDocumentsTab.reload();
}
if (newId) {
    document.dispatchEvent(new CustomEvent('patient:saved', {
        detail: { patientId: newId }
    }));
}

// 🔹 Show the "Saved Successfully" modal
const modalEl = document.getElementById('saveSuccessModal');
if (modalEl && window.bootstrap && bootstrap.Modal) {
    const saveModal = bootstrap.Modal.getOrCreateInstance(modalEl);
    saveModal.show();
}
    } catch (err) {
        console.error(err);
        if (msg) msg.textContent = 'An unexpected error occurred while saving patient details.';
    }
}

    document.addEventListener('DOMContentLoaded', async function() {
        const msg = document.getElementById('basicDetailsMessage');
        const birthInput = document.getElementById('PatientBirthDate');
        const btnSave = document.getElementById('btnSavePatientMain');

        const patientId = getRootPatientId();

        await loadLookups();
        await loadPatientBasic(patientId);

        if (birthInput) {
            birthInput.addEventListener('change', computeAgeFromBirthDateInput);
        }

        if (btnSave) {
            btnSave.addEventListener('click', saveBasic);
        }

        if (msg) msg.textContent = '';
    });
})();