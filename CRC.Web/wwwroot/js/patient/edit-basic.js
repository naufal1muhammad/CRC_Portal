// @ts-nocheck
(function () {
    let lookupsLoaded = false;
    let statesLoaded = false;

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

    function show(el, visible) {
        if (!el) return;
        el.style.display = visible ? '' : 'none';
    }

    function resetSelect(select, placeholder) {
        if (!select) return;
        select.innerHTML = '';
        const opt = document.createElement('option');
        opt.value = '';
        opt.textContent = placeholder || '-- Select --';
        select.appendChild(opt);
        select.value = '';
    }

    function setSelectOptions(select, items, placeholder) {
        if (!select) return;

        resetSelect(select, placeholder);

        items.forEach(item => {
            const opt = document.createElement('option');
            opt.value = (item.id ?? '').toString();
            opt.textContent = item.name ?? '';
            select.appendChild(opt);
        });
    }

    // For location selects: option value = id, but we store "name" into PatientBasic
    function setSelectOptionsWithDataName(select, items, placeholder) {
        if (!select) return;

        resetSelect(select, placeholder);

        items.forEach(item => {
            const opt = document.createElement('option');
            opt.value = (item.id ?? '').toString();
            opt.textContent = item.name ?? '';
            opt.setAttribute('data-name', item.name ?? '');
            select.appendChild(opt);
        });
    }

    function getSelectedDataName(select) {
        if (!select) return '';
        const idx = select.selectedIndex;
        if (idx < 0) return '';
        const opt = select.options[idx];
        return (opt && opt.getAttribute('data-name')) ? opt.getAttribute('data-name') : (opt ? opt.textContent : '');
    }

    function selectOptionByDataName(select, targetName) {
        if (!select || !targetName) return '';
        const t = targetName.trim().toLowerCase();

        for (let i = 0; i < select.options.length; i++) {
            const opt = select.options[i];
            const dn = (opt.getAttribute('data-name') || opt.textContent || '').trim().toLowerCase();
            if (dn === t) {
                select.selectedIndex = i;
                return opt.value || '';
            }
        }
        return '';
    }

    function calculateAgeFromDate(dob) {
        if (!dob || isNaN(dob.getTime())) return '';
        const today = new Date();
        let age = today.getFullYear() - dob.getFullYear();
        const m = today.getMonth() - dob.getMonth();
        if (m < 0 || (m === 0 && today.getDate() < dob.getDate())) age--;
        return (age >= 0 && age <= 150) ? age : '';
    }

    function deriveFromNric(nricDigits) {
        if (!nricDigits || nricDigits.length !== 12 || !/^[0-9]{12}$/.test(nricDigits)) return null;

        const yy = parseInt(nricDigits.substring(0, 2), 10);
        const mm = parseInt(nricDigits.substring(2, 4), 10);
        const dd = parseInt(nricDigits.substring(4, 6), 10);

        const currentYY = new Date().getFullYear() % 100;
        const year = (yy <= currentYY) ? (2000 + yy) : (1900 + yy);

        // Validate date
        const dob = new Date(year, mm - 1, dd);
        if (dob.getFullYear() !== year || dob.getMonth() !== (mm - 1) || dob.getDate() !== dd) return null;

        const birthDateStr =
            year.toString().padStart(4, '0') + '-' +
            mm.toString().padStart(2, '0') + '-' +
            dd.toString().padStart(2, '0');

        const lastDigit = parseInt(nricDigits.substring(11, 12), 10);
        const gender = (lastDigit % 2 === 1) ? 'MALE' : 'FEMALE';

        const age = calculateAgeFromDate(dob);

        return { birthDateStr, gender, age };
    }

    function applyDerivedFieldsFromNric() {
        const nricInput = document.getElementById('PatientNRIC');
        const birthInput = document.getElementById('PatientBirthDate');
        const ageInput = document.getElementById('PatientAge');
        const genderSelect = document.getElementById('PatientGender');

        if (!nricInput) return;

        const digits = (nricInput.value || '').replace(/\D/g, '');
        if (digits.length !== 12) {
            if (birthInput) birthInput.value = '';
            if (ageInput) ageInput.value = '';
            if (genderSelect) genderSelect.value = '';
            return;
        }

        const derived = deriveFromNric(digits);
        if (!derived) {
            if (birthInput) birthInput.value = '';
            if (ageInput) ageInput.value = '';
            if (genderSelect) genderSelect.value = '';
            return;
        }

        if (birthInput) birthInput.value = derived.birthDateStr;
        if (ageInput) ageInput.value = derived.age.toString();
        if (genderSelect) genderSelect.value = derived.gender;
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

            setSelectOptions(document.getElementById('PatientRace'), result.races || [], '-- Select Race --');
            setSelectOptions(document.getElementById('PatientSource'), result.sources || [], '-- Select Source --');
            setSelectOptions(document.getElementById('PatientReligion'), result.religions || [], '-- Select Religion --');
            setSelectOptions(document.getElementById('PatientMaritalStatus'), result.maritalStatuses || [], '-- Select Marital Status --');
            setSelectOptions(document.getElementById('PatientOccupation'), result.occupations || [], '-- Select Occupation --');

            lookupsLoaded = true;
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'Error loading lookups.';
        }
    }

    async function loadStates() {
        const stateSelect = document.getElementById('PatientResState');

        try {
            const response = await fetch('/Patient/GetStates', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                resetSelect(stateSelect, '-- Select State --');
                return;
            }

            const result = await response.json();
            if (!result.success) {
                resetSelect(stateSelect, '-- Select State --');
                return;
            }

            setSelectOptionsWithDataName(stateSelect, result.data || [], '-- Select State --');
            statesLoaded = true;
        } catch (err) {
            console.error(err);
            resetSelect(stateSelect, '-- Select State --');
        }
    }

    async function loadCitiesByState(stateId) {
        const citySelect = document.getElementById('PatientResCity');
        resetSelect(citySelect, '-- Select City --');

        if (!stateId) return;

        try {
            const response = await fetch('/Patient/GetCitiesByState?stateId=' + encodeURIComponent(stateId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) return;

            const result = await response.json();
            if (!result.success) return;

            setSelectOptionsWithDataName(citySelect, result.data || [], '-- Select City --');
        } catch (err) {
            console.error(err);
        }
    }

    async function loadPostcodesByCity(cityId) {
        const postcodeSelect = document.getElementById('PatientResPostcode');
        resetSelect(postcodeSelect, '-- Select Postcode --');

        if (!cityId) return;

        try {
            const response = await fetch('/Patient/GetPostcodesByCity?cityId=' + encodeURIComponent(cityId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) return;

            const result = await response.json();
            if (!result.success) return;

            setSelectOptionsWithDataName(postcodeSelect, result.data || [], '-- Select Postcode --');
        } catch (err) {
            console.error(err);
        }
    }

    function initDischargeFromBasic(patient) {
        if (!window.PatientDischargeTab || typeof window.PatientDischargeTab.init !== 'function') return;

        if (!patient) {
            window.PatientDischargeTab.init(null);
            return;
        }

        const hasDischarge = !!(patient.dischargeTypeId || patient.dischargeTypeName);

        window.PatientDischargeTab.init({
            isDischarged: hasDischarge,
            dischargeTypeId: patient.dischargeTypeId || null,
            dischargeTypeName: patient.dischargeTypeName || '',
            dischargeDate: patient.dischargeDate || '',
            dischargeRemarks: patient.dischargeRemarks || ''
        });
    }

    async function applyResidentialSelectionsFromPatient(patient) {
        const stateSelect = document.getElementById('PatientResState');
        const citySelect = document.getElementById('PatientResCity');
        const postcodeSelect = document.getElementById('PatientResPostcode');

        const cityContainer = document.getElementById('resCityContainer');
        const postcodeContainer = document.getElementById('resPostcodeContainer');
        const addLine1Container = document.getElementById('addLine1Container');
        const addLine2Container = document.getElementById('addLine2Container');

        // Default hide
        show(cityContainer, false);
        show(postcodeContainer, false);
        show(addLine1Container, false);
        show(addLine2Container, false);

        resetSelect(citySelect, '-- Select City --');
        resetSelect(postcodeSelect, '-- Select Postcode --');

        if (!patient || !patient.resState) return;

        // Ensure states loaded
        if (!statesLoaded) {
            await loadStates();
        }

        const stateId = selectOptionByDataName(stateSelect, patient.resState);
        if (!stateId) return;

        show(cityContainer, true);
        await loadCitiesByState(stateId);

        if (patient.resCity) {
            const cityId = selectOptionByDataName(citySelect, patient.resCity);
            if (cityId) {
                show(postcodeContainer, true);
                await loadPostcodesByCity(cityId);

                if (patient.resPostcode) {
                    const postcodeId = selectOptionByDataName(postcodeSelect, patient.resPostcode);
                    if (postcodeId) {
                        show(addLine1Container, true);
                        show(addLine2Container, true);
                    }
                }
            }
        }
    }

    async function loadPatientBasic(patientId) {
        const msg = document.getElementById('basicDetailsMessage');

        // For new patient page
        if (!patientId) {
            document.getElementById('patientHeaderName').textContent = 'Patient: -';
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

            // Update hidden + root
            const hidId = document.getElementById('PatientIdHidden');
            if (hidId) hidId.value = p.patientId || '';
            setRootPatientId(p.patientId || '');

            // Header
            const headerName = document.getElementById('patientHeaderName');
            const headerId = document.getElementById('patientHeaderId');
            if (headerId) headerId.textContent = p.patientId ? ('ID: ' + p.patientId) : '';
            if (headerName) headerName.textContent = 'Patient: ' + (p.name || '-');

            // Basic fields
            document.getElementById('PatientName').value = p.name || '';
            document.getElementById('PatientEmail').value = p.email || '';
            document.getElementById('PatientPhone').value = p.phone || '';
            document.getElementById('PatientNRIC').value = p.nric || '';

            document.getElementById('PatientBirthDate').value = p.birthDate || '';
            document.getElementById('PatientAge').value = (p.age !== undefined && p.age !== null) ? p.age : '';
            document.getElementById('PatientGender').value = p.gender || '';

            // Lookup selects (store IDs)
            if (lookupsLoaded) {
                document.getElementById('PatientRace').value = p.raceId || '';
                document.getElementById('PatientSource').value = p.sourceId || '';
                document.getElementById('PatientReligion').value = p.religionId || '';
                document.getElementById('PatientMaritalStatus').value = p.maritalStatusId || '';
                document.getElementById('PatientOccupation').value = p.occupationId || '';
            }

            // Residential
            document.getElementById('PatientAddLine1').value = p.addLine1 || '';
            document.getElementById('PatientAddLine2').value = p.addLine2 || '';

            await applyResidentialSelectionsFromPatient(p);

            // Emergency
            document.getElementById('PatientEmergencyName').value = p.emergencyName || '';
            document.getElementById('PatientEmergencyRelationship').value = p.emergencyRelationship || '';
            document.getElementById('PatientEmergencyNumber').value = p.emergencyNumber || '';

            // Discharge tab
            initDischargeFromBasic(p);

            // Ensure NRIC-derived fields are consistent (optional)
            applyDerivedFieldsFromNric();
        } catch (err) {
            console.error(err);
            if (msg) msg.textContent = 'Error loading patient details.';
        }
    }

    function buildBasicPayload() {
        const msg = document.getElementById('basicDetailsMessage');
        if (msg) {
            msg.textContent = '';
            msg.classList.remove('text-success');
            msg.classList.add('text-danger');
        }

        const hidId = document.getElementById('PatientIdHidden');

        const name = (document.getElementById('PatientName').value || '').trim();
        const email = (document.getElementById('PatientEmail').value || '').trim();
        const phone = (document.getElementById('PatientPhone').value || '').trim();
        const nricRaw = (document.getElementById('PatientNRIC').value || '').trim();
        const nricDigits = nricRaw.replace(/\D/g, '');

        const raceId = (document.getElementById('PatientRace').value || '').trim();
        const sourceId = (document.getElementById('PatientSource').value || '').trim();
        const religionId = (document.getElementById('PatientReligion').value || '').trim();
        const maritalStatusId = (document.getElementById('PatientMaritalStatus').value || '').trim();
        const occupationId = (document.getElementById('PatientOccupation').value || '').trim();

        const stateSelect = document.getElementById('PatientResState');
        const citySelect = document.getElementById('PatientResCity');
        const postcodeSelect = document.getElementById('PatientResPostcode');

        const resState = (getSelectedDataName(stateSelect) || '').trim();
        const resCity = (getSelectedDataName(citySelect) || '').trim();
        const resPostcode = (getSelectedDataName(postcodeSelect) || '').trim();

        const addLine1 = (document.getElementById('PatientAddLine1').value || '').trim();
        const addLine2 = (document.getElementById('PatientAddLine2').value || '').trim();

        const emergencyName = (document.getElementById('PatientEmergencyName').value || '').trim();
        const emergencyRelationship = (document.getElementById('PatientEmergencyRelationship').value || '').trim();
        const emergencyNumber = (document.getElementById('PatientEmergencyNumber').value || '').trim();

        // Validation
        if (!name || !email || !phone || !nricDigits || !raceId || !sourceId || !religionId || !maritalStatusId || !occupationId ||
            !resState || !resCity || !resPostcode || !addLine1 || !emergencyName || !emergencyRelationship || !emergencyNumber) {
            if (msg) msg.textContent = 'Please fill in all mandatory fields.';
            return null;
        }

        if (!/^[0-9]{12}$/.test(nricDigits)) {
            if (msg) msg.textContent = 'NRIC must be exactly 12 digits.';
            return null;
        }

        const derived = deriveFromNric(nricDigits);
        if (!derived) {
            if (msg) msg.textContent = 'Invalid NRIC (unable to derive Birth Date / Gender).';
            return null;
        }

        return {
            patientId: hidId ? (hidId.value || '').trim() : '',
            name: name,
            email: email,
            phone: phone,
            nric: nricDigits,

            raceId: raceId,
            sourceId: sourceId,
            religionId: religionId,
            maritalStatusId: maritalStatusId,
            occupationId: occupationId,

            resState: resState,
            resCity: resCity,
            resPostcode: resPostcode,
            addLine1: addLine1,
            addLine2: addLine2,

            emergencyName: emergencyName,
            emergencyRelationship: emergencyRelationship,
            emergencyNumber: emergencyNumber
        };
    }

    async function saveBasic() {
        const msg = document.getElementById('basicDetailsMessage');

        const basicData = buildBasicPayload();
        if (!basicData) return;

        // Get discharge payload
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
                if (msg) msg.textContent = 'Please fix the errors in the Discharge tab before saving.';
                return;
            }
            discharge = d;
        }

        const dataToSend = {
            ...basicData,
            isDischarged: discharge.isDischarged,
            dischargeTypeId: discharge.dischargeTypeId,
            dischargeTypeName: discharge.dischargeTypeName,
            dischargeDate: discharge.dischargeDate,
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

            // Update hidden + root
            const hidId = document.getElementById('PatientIdHidden');
            if (hidId && newId) hidId.value = newId;
            setRootPatientId(newId);

            // Update header
            const headerName = document.getElementById('patientHeaderName');
            const headerId = document.getElementById('patientHeaderId');
            if (headerId && newId) headerId.textContent = 'ID: ' + newId;
            if (headerName && basicData.name) headerName.textContent = 'Patient: ' + basicData.name;

            // Clear message area
            if (msg) {
                msg.textContent = '';
                msg.classList.remove('text-success');
                msg.classList.add('text-danger');
            }

            // Enable "Add Appointment" / "Add Journey" buttons (existing behavior)
            const btnAddAppointment = document.getElementById('btnAddAppointment');
            if (btnAddAppointment) btnAddAppointment.disabled = false;

            const btnAddJourney = document.getElementById('btnAddJourney');
            if (btnAddJourney) btnAddJourney.disabled = false;

            // Refresh documents tab if available
            if (window.PatientDocumentsTab && typeof window.PatientDocumentsTab.reload === 'function') {
                window.PatientDocumentsTab.reload();
            }

            if (newId) {
                document.dispatchEvent(new CustomEvent('patient:saved', {
                    detail: { patientId: newId }
                }));
            }

            // Show "Saved Successfully" modal
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

    async function onStateChanged() {
        const stateSelect = document.getElementById('PatientResState');
        const citySelect = document.getElementById('PatientResCity');
        const postcodeSelect = document.getElementById('PatientResPostcode');

        const cityContainer = document.getElementById('resCityContainer');
        const postcodeContainer = document.getElementById('resPostcodeContainer');
        const addLine1Container = document.getElementById('addLine1Container');
        const addLine2Container = document.getElementById('addLine2Container');

        // Reset downstream
        show(cityContainer, false);
        show(postcodeContainer, false);
        show(addLine1Container, false);
        show(addLine2Container, false);

        resetSelect(citySelect, '-- Select City --');
        resetSelect(postcodeSelect, '-- Select Postcode --');

        const stateId = stateSelect ? stateSelect.value : '';
        if (!stateId) return;

        show(cityContainer, true);
        await loadCitiesByState(stateId);
    }

    async function onCityChanged() {
        const citySelect = document.getElementById('PatientResCity');
        const postcodeSelect = document.getElementById('PatientResPostcode');

        const postcodeContainer = document.getElementById('resPostcodeContainer');
        const addLine1Container = document.getElementById('addLine1Container');
        const addLine2Container = document.getElementById('addLine2Container');

        show(postcodeContainer, false);
        show(addLine1Container, false);
        show(addLine2Container, false);

        resetSelect(postcodeSelect, '-- Select Postcode --');

        const cityId = citySelect ? citySelect.value : '';
        if (!cityId) return;

        show(postcodeContainer, true);
        await loadPostcodesByCity(cityId);
    }

    function onPostcodeChanged() {
        const postcodeSelect = document.getElementById('PatientResPostcode');
        const addLine1Container = document.getElementById('addLine1Container');
        const addLine2Container = document.getElementById('addLine2Container');

        show(addLine1Container, false);
        show(addLine2Container, false);

        const postcodeId = postcodeSelect ? postcodeSelect.value : '';
        if (!postcodeId) return;

        show(addLine1Container, true);
        show(addLine2Container, true);
    }

    document.addEventListener('DOMContentLoaded', async function () {
        const msg = document.getElementById('basicDetailsMessage');
        const btnSave = document.getElementById('btnSavePatientMain');

        const nricInput = document.getElementById('PatientNRIC');
        const stateSelect = document.getElementById('PatientResState');
        const citySelect = document.getElementById('PatientResCity');
        const postcodeSelect = document.getElementById('PatientResPostcode');

        const patientId = getRootPatientId();

        await loadLookups();
        await loadStates();
        await loadPatientBasic(patientId);

        // Wire events
        if (nricInput) {
            nricInput.addEventListener('input', applyDerivedFieldsFromNric);
            nricInput.addEventListener('change', applyDerivedFieldsFromNric);
        }

        if (stateSelect) {
            stateSelect.addEventListener('change', onStateChanged);
        }
        if (citySelect) {
            citySelect.addEventListener('change', onCityChanged);
        }
        if (postcodeSelect) {
            postcodeSelect.addEventListener('change', onPostcodeChanged);
        }

        if (btnSave) {
            btnSave.addEventListener('click', saveBasic);
        }

        if (msg) msg.textContent = '';
    });
})();