// @ts-nocheck
// Read-only "Basic Details" loader for the STAFF user's My Profile page.
// Pulls the staff record from /Staff/GetStaff (which is staff-accessible only
// for the caller's own StaffId) and resolves the state/city/postcode names
// via /MyProfileStaff/GetLocationNames.
(function () {
    function qs(id) {
        return document.getElementById(id);
    }

    function getStaffId() {
        const root = document.querySelector('[data-staff-id]');
        return root ? (root.getAttribute('data-staff-id') || '').trim() : '';
    }

    function setValue(id, value) {
        const el = qs(id);
        if (!el) return;
        el.value = (value ?? '').toString();
    }

    function setMessage(text) {
        const el = qs('staffBasicMessage');
        if (!el) return;
        el.textContent = text || '';
    }

    function setHeader(name, id) {
        const headerName = qs('staffHeaderName');
        if (headerName) {
            headerName.textContent = name ? 'My Profile: ' + name : 'My Profile';
        }
        const headerId = qs('staffHeaderId');
        if (headerId && id) {
            headerId.textContent = 'ID: ' + id;
        }
    }

    async function fetchStaff(staffId) {
        const response = await fetch('/Staff/GetStaff?staffId=' + encodeURIComponent(staffId), {
            method: 'GET',
            headers: { 'Accept': 'application/json' }
        });

        if (!response.ok) {
            throw new Error('HTTP ' + response.status);
        }

        const result = await response.json();
        if (!result || !result.success || !result.data) {
            throw new Error((result && result.message) || 'Failed to load profile.');
        }
        return result.data;
    }

    async function fetchLocationNames(stateId, cityId, postcodeId) {
        const params = new URLSearchParams();
        if (stateId) params.set('stateId', stateId);
        if (cityId) params.set('cityId', cityId);
        if (postcodeId) params.set('postcodeId', postcodeId);

        const response = await fetch('/MyProfileStaff/GetLocationNames?' + params.toString(), {
            method: 'GET',
            headers: { 'Accept': 'application/json' }
        });

        if (!response.ok) {
            return { stateName: '', cityName: '', postcodeName: '' };
        }

        const result = await response.json();
        if (!result || !result.success) {
            return { stateName: '', cityName: '', postcodeName: '' };
        }
        return {
            stateName: result.stateName || '',
            cityName: result.cityName || '',
            postcodeName: result.postcodeName || ''
        };
    }

    function populate(data, locationNames) {
        setValue('StaffId', data.staffId);
        setValue('StaffName', data.name);
        setValue('StaffNRIC', data.nric);
        setValue('StaffBirthDate', data.birthDate);
        setValue('StaffAge', data.age);
        setValue('StaffPhone', data.phone);
        setValue('StaffEmail', data.email);
        setValue('StaffGender', (data.gender || '').toUpperCase());

        setValue('StaffResState', locationNames.stateName || data.resState || '');
        setValue('StaffResCity', locationNames.cityName || data.resCity || '');
        setValue('StaffResPostcode', locationNames.postcodeName || data.resPostcode || '');

        setValue('StaffAddLine1', data.addLine1);
        setValue('StaffAddLine2', data.addLine2);

        setValue('StaffBase', data.staffBase);
        setValue('StaffType', data.staffTypeName || data.staffTypeId || '');

        setHeader(data.name, data.staffId);
    }

    async function load() {
        const staffId = getStaffId();
        if (!staffId) {
            setMessage('Your user is not linked to a Staff record.');
            return;
        }

        try {
            const data = await fetchStaff(staffId);
            const locationNames = await fetchLocationNames(data.resState, data.resCity, data.resPostcode);
            populate(data, locationNames);
            setMessage('');
        } catch (err) {
            console.error(err);
            setMessage((err && err.message) || 'Failed to load profile.');
        }
    }

    document.addEventListener('DOMContentLoaded', load);
})();
