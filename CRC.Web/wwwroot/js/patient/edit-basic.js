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

    function setDependentTabsDisabled(disabled) {
        const tabIds = ['tab-appointment', 'tab-journey', 'tab-documents', 'tab-discharge-tab'];
        tabIds.forEach(id => {
            const tab = document.getElementById(id);
            if (!tab) return;
            tab.disabled = disabled;
            tab.setAttribute('aria-disabled', disabled ? 'true' : 'false');
            tab.classList.toggle('disabled', disabled);
        });
    }

    function updateDependentTabs() {
        const hasPatient = !!getRootPatientId();
        setDependentTabsDisabled(!hasPatient);
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

        refreshSelect2(select);
    }

    function setSelectDisabled(select, isDisabled) {
        if (!select) return;
        select.disabled = !!isDisabled;
        if (window.$ && $.fn.select2 && $(select).hasClass('select2-hidden-accessible')) {
            $(select).prop('disabled', !!isDisabled).trigger('change.select2');
        }
    }

    // When Select2 is applied, setting `select.value` alone does not update the UI.
    // Select2 listens on `change.select2`, so we trigger that to refresh the displayed selection.
    function refreshSelect2(select) {
        if (!select) return;
        if (window.$ && $.fn.select2 && $(select).hasClass('select2-hidden-accessible')) {
            $(select).trigger('change.select2');
        }
    }

    function selectOptionByText(select, text) {
        if (!select || !text) return;
        const target = text.trim().toLowerCase();

        for (let i = 0; i < select.options.length; i++) {
            const opt = select.options[i];
            if ((opt.textContent || '').trim().toLowerCase() === target) {
                select.value = opt.value;
                refreshSelect2(select);
                return;
            }
        }
    }

    function getSelectedText(select) {
        if (!select) return '';
        if (!select.value) return '';
        if (select.selectedIndex < 0) return '';
        return select.options[select.selectedIndex].text || '';
    }

    function updateAddressLineInputsEnabled() {
        const postcodeSelect = document.getElementById('PatientResPostcode');
        const addLine1 = document.getElementById('PatientAddLine1');
        const addLine2 = document.getElementById('PatientAddLine2');

        const enabled = !!(postcodeSelect && postcodeSelect.value);
        if (addLine1) addLine1.disabled = !enabled;
        if (addLine2) addLine2.disabled = !enabled;
    }

    function applySelect2() {
        if (!(window.$ && $.fn.select2)) return;

        const select2Config = {
            theme: 'bootstrap-5',
            width: '100%',
            allowClear: true
        };

        const stateSelect = $('#PatientResState');
        const citySelect = $('#PatientResCity');
        const postcodeSelect = $('#PatientResPostcode');

        if (stateSelect.length) {
            if (stateSelect.hasClass('select2-hidden-accessible')) {
                stateSelect.select2('destroy');
            }
            stateSelect.select2({
                ...select2Config,
                placeholder: '-- Select State --'
            });
        }

        if (citySelect.length) {
            if (citySelect.hasClass('select2-hidden-accessible')) {
                citySelect.select2('destroy');
            }
            citySelect.select2({
                ...select2Config,
                placeholder: '-- Select City --'
            });
        }

        if (postcodeSelect.length) {
            if (postcodeSelect.hasClass('select2-hidden-accessible')) {
                postcodeSelect.select2('destroy');
            }
            postcodeSelect.select2({
                ...select2Config,
                placeholder: '-- Select Postcode --'
            });
        }
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
        const citySelect = document.getElementById('PatientResCity');
        const postcodeSelect = document.getElementById('PatientResPostcode');

        try {
            const response = await fetch('/Patient/GetStates', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                resetSelect(stateSelect, '-- Select State --');
                setSelectDisabled(citySelect, true);
                setSelectOptions(citySelect, [], '-- Select City --');
                setSelectDisabled(postcodeSelect, true);
                setSelectOptions(postcodeSelect, [], '-- Select Postcode --');
                updateAddressLineInputsEnabled();
                return;
            }

            const result = await response.json();
            if (!result.success) {
                resetSelect(stateSelect, '-- Select State --');
                setSelectDisabled(citySelect, true);
                setSelectOptions(citySelect, [], '-- Select City --');
                setSelectDisabled(postcodeSelect, true);
                setSelectOptions(postcodeSelect, [], '-- Select Postcode --');
                updateAddressLineInputsEnabled();
                return;
            }

            setSelectOptions(stateSelect, result.data || [], '-- Select State --');
            setSelectDisabled(citySelect, true);
            setSelectOptions(citySelect, [], '-- Select City --');
            setSelectDisabled(postcodeSelect, true);
            setSelectOptions(postcodeSelect, [], '-- Select Postcode --');
            updateAddressLineInputsEnabled();
            statesLoaded = true;
        } catch (err) {
            console.error(err);
            resetSelect(stateSelect, '-- Select State --');
            setSelectDisabled(citySelect, true);
            setSelectOptions(citySelect, [], '-- Select City --');
            setSelectDisabled(postcodeSelect, true);
            setSelectOptions(postcodeSelect, [], '-- Select Postcode --');
            updateAddressLineInputsEnabled();
        }
    }

    async function loadCitiesByState(stateId) {
        const citySelect = document.getElementById('PatientResCity');
        const postcodeSelect = document.getElementById('PatientResPostcode');

        setSelectDisabled(citySelect, true);
        setSelectOptions(citySelect, [], '-- Select City --');
        setSelectDisabled(postcodeSelect, true);
        setSelectOptions(postcodeSelect, [], '-- Select Postcode --');
        updateAddressLineInputsEnabled();

        if (!stateId) return;

        try {
            const response = await fetch('/Patient/GetCitiesByState?stateId=' + encodeURIComponent(stateId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) return;

            const result = await response.json();
            if (!result.success) return;

            setSelectOptions(citySelect, result.data || [], '-- Select City --');
            setSelectDisabled(citySelect, false);
        } catch (err) {
            console.error(err);
        }
    }

    async function loadPostcodesByCity(cityId) {
        const postcodeSelect = document.getElementById('PatientResPostcode');
        setSelectDisabled(postcodeSelect, true);
        setSelectOptions(postcodeSelect, [], '-- Select Postcode --');
        updateAddressLineInputsEnabled();

        if (!cityId) return;

        try {
            const response = await fetch('/Patient/GetPostcodesByCity?cityId=' + encodeURIComponent(cityId), {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) return;

            const result = await response.json();
            if (!result.success) return;

            setSelectOptions(postcodeSelect, result.data || [], '-- Select Postcode --');
            setSelectDisabled(postcodeSelect, false);
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

        // Reset downstream + disable until selected
        setSelectDisabled(citySelect, true);
        setSelectOptions(citySelect, [], '-- Select City --');
        setSelectDisabled(postcodeSelect, true);
        setSelectOptions(postcodeSelect, [], '-- Select Postcode --');
        updateAddressLineInputsEnabled();

        if (!patient) return;

        // Ensure states loaded
        if (!statesLoaded) {
            await loadStates();
        }

        // State -> City -> Postcode
        if (patient.resState) {
            selectOptionByText(stateSelect, patient.resState);
            const stateId = stateSelect?.value || '';
            if (stateId) {
                await loadCitiesByState(stateId);
            }
        }

        if (patient.resCity) {
            selectOptionByText(citySelect, patient.resCity);
            const cityId = citySelect?.value || '';
            if (cityId) {
                await loadPostcodesByCity(cityId);
            }
        }

        if (patient.resPostcode) {
            selectOptionByText(postcodeSelect, patient.resPostcode);
        }

        updateAddressLineInputsEnabled();
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

        const resState = (getSelectedText(stateSelect) || '').trim();
        const resCity = (getSelectedText(citySelect) || '').trim();
        const resPostcode = (getSelectedText(postcodeSelect) || '').trim();

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

            updateDependentTabs();

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
        await loadCitiesByState(stateSelect ? stateSelect.value : '');
    }

    async function onCityChanged() {
        const citySelect = document.getElementById('PatientResCity');
        await loadPostcodesByCity(citySelect ? citySelect.value : '');
    }

    function onPostcodeChanged() {
        updateAddressLineInputsEnabled();
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
        applySelect2();
        await loadPatientBasic(patientId);
        updateDependentTabs();

        // Wire events
        if (nricInput) {
            nricInput.addEventListener('input', applyDerivedFieldsFromNric);
            nricInput.addEventListener('change', applyDerivedFieldsFromNric);
        }

        // Address: State -> City -> Postcode (Select2 where available)
        if (window.$ && $.fn.select2) {
            $('#PatientResState')
                .off('change.patientbasic')
                .on('change.patientbasic', async function () {
                    await loadCitiesByState(this.value);
                });

            $('#PatientResCity')
                .off('change.patientbasic')
                .on('change.patientbasic', async function () {
                    await loadPostcodesByCity(this.value);
                });

            $('#PatientResPostcode')
                .off('change.patientbasic')
                .on('change.patientbasic', onPostcodeChanged);
        } else {
            if (stateSelect) stateSelect.addEventListener('change', onStateChanged);
            if (citySelect) citySelect.addEventListener('change', onCityChanged);
            if (postcodeSelect) postcodeSelect.addEventListener('change', onPostcodeChanged);
        }

        updateAddressLineInputsEnabled();

        if (btnSave) {
            btnSave.addEventListener('click', saveBasic);
        }

        if (msg) msg.textContent = '';
    });
})();