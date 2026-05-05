// @ts-nocheck
(function () {
    'use strict';

    // Cached server data
    let appointmentTypes = [];   // [{ pjAppTypeId, pjAppTypeName }]
    let patients = [];           // [{ patientId, name, nric, phone, age, gender, dischargeDate, isStalled }]
    // Map: patientId -> Map(pjAppTypeId -> { status, date })
    let appointmentsByPatient = new Map();
    // Map: patientId -> [{ pjAppTypeName, date }]
    let proceduresByPatient = new Map();

    // UI state
    let searchTerm = '';
    let activeTypeFilters = new Set(); // PjAppType_ID values; empty = no filter

    // DOM
    let searchInput;
    let filterChipsEl;
    let listEl;
    let stalledCountEl;

    // ---------- helpers ----------
    function escapeHtml(s) {
        if (s === null || s === undefined) return '';
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function statusToCss(status) {
        switch ((status || '').toLowerCase()) {
            case 'scheduled': return { cell: 'is-scheduled', pill: 's-scheduled' };
            case 'attended': return { cell: 'is-attended', pill: 's-attended' };
            case 'not attended': return { cell: 'is-not-attended', pill: 's-not-attended' };
            case 'recorded': return { cell: 'is-recorded', pill: 's-recorded' };
            default: return { cell: 'is-not-booked', pill: 's-not-booked' };
        }
    }

    // ---------- data load ----------
    async function loadTrackerData() {
        try {
            const res = await fetch('/PatientTracker/GetTrackerData', {
                method: 'GET',
                headers: { 'Accept': 'application/json' },
                cache: 'no-store'
            });

            if (!res.ok) {
                listEl.innerHTML = `<div class="tracker-empty text-danger">Error loading tracker data.</div>`;
                return;
            }

            const data = await res.json();
            if (!data || data.success === false) {
                listEl.innerHTML = `<div class="tracker-empty text-danger">${escapeHtml((data && data.message) || 'Error loading tracker data.')}</div>`;
                return;
            }

            appointmentTypes = Array.isArray(data.appointmentTypes) ? data.appointmentTypes : [];
            patients = Array.isArray(data.patients) ? data.patients : [];

            // Index appointments by patient + type
            appointmentsByPatient = new Map();
            (data.appointments || []).forEach(a => {
                if (!appointmentsByPatient.has(a.patientId)) {
                    appointmentsByPatient.set(a.patientId, new Map());
                }
                appointmentsByPatient.get(a.patientId).set(a.pjAppTypeId, {
                    status: a.status,
                    date: a.date
                });
            });

            // Index procedures by patient (preserve order: SP returns sorted asc by date)
            proceduresByPatient = new Map();
            (data.procedures || []).forEach(p => {
                if (!proceduresByPatient.has(p.patientId)) {
                    proceduresByPatient.set(p.patientId, []);
                }
                proceduresByPatient.get(p.patientId).push({
                    pjAppTypeName: p.pjAppTypeName,
                    date: p.date
                });
            });

            stalledCountEl.textContent = String(data.stalledCount || 0);

            renderFilterChips();
            renderPatientList();
        } catch (err) {
            console.error('Error loading tracker data', err);
            listEl.innerHTML = `<div class="tracker-empty text-danger">An unexpected error occurred while loading the tracker.</div>`;
        }
    }

    // ---------- filter chips ----------
    function renderFilterChips() {
        if (!appointmentTypes.length) {
            filterChipsEl.innerHTML = '<span class="text-muted small">No appointment types configured.</span>';
            return;
        }

        const html = appointmentTypes.map(t => {
            const isActive = activeTypeFilters.has(t.pjAppTypeId);
            return `
                <button type="button"
                        class="tracker-filter-chip ${isActive ? 'active' : ''}"
                        data-type-id="${escapeHtml(t.pjAppTypeId)}">
                    ${escapeHtml(t.pjAppTypeName)}
                </button>
            `;
        }).join('');

        filterChipsEl.innerHTML = html;

        filterChipsEl.querySelectorAll('.tracker-filter-chip').forEach(btn => {
            btn.addEventListener('click', () => {
                const typeId = btn.getAttribute('data-type-id');
                if (!typeId) return;

                if (activeTypeFilters.has(typeId)) {
                    activeTypeFilters.delete(typeId);
                    btn.classList.remove('active');
                } else {
                    activeTypeFilters.add(typeId);
                    btn.classList.add('active');
                }
                renderPatientList();
            });
        });
    }

    // ---------- patient list ----------
    function patientMatchesSearch(p) {
        if (!searchTerm) return true;
        const q = searchTerm.toLowerCase();
        return (
            (p.name || '').toLowerCase().includes(q) ||
            (p.patientId || '').toLowerCase().includes(q) ||
            (p.nric || '').toLowerCase().includes(q)
        );
    }

    function patientMatchesFilters(p) {
        if (activeTypeFilters.size === 0) return true;

        // A patient is shown if they have ANY appointment record for at least
        // one of the selected types (i.e. status != 'Not Booked' for that type).
        const apptMap = appointmentsByPatient.get(p.patientId);
        if (!apptMap) return false;

        for (const typeId of activeTypeFilters) {
            if (apptMap.has(typeId)) return true;
        }
        return false;
    }

    function renderPatientList() {
        const filtered = patients.filter(p => patientMatchesSearch(p) && patientMatchesFilters(p));

        if (filtered.length === 0) {
            listEl.innerHTML = `<div class="tracker-empty">No patients match the current search and filters.</div>`;
            return;
        }

        listEl.innerHTML = filtered.map(p => renderPatientCard(p)).join('');
    }

    function renderPatientCard(p) {
        return `
            <div class="tracker-patient-card" data-patient-id="${escapeHtml(p.patientId)}">
                <div class="tracker-card-body">
                    ${renderPatientInfo(p)}
                    <div class="tracker-journeys">
                        ${renderAppointmentJourney(p)}
                        ${renderProcedureJourney(p)}
                    </div>
                </div>
            </div>
        `;
    }

    function renderPatientInfo(p) {
        const dischargePill = p.dischargeDate
            ? `<div class="tracker-discharge-pill">Discharged: ${escapeHtml(p.dischargeDate)}</div>`
            : '';
        const stalledPill = p.isStalled
            ? `<div class="tracker-stalled-pill">Stalled</div>`
            : '';

        const nameHtml = p.patientId
            ? `<a href="/Patient/Edit/${encodeURIComponent(p.patientId)}" class="info-name-link">${escapeHtml(p.name || '-')}</a>`
            : escapeHtml(p.name || '-');

        return `
            <div class="tracker-patient-info">
                <div class="info-name">${nameHtml}</div>
                <div class="info-row"><span class="info-label">Patient ID</span><span class="info-value">${escapeHtml(p.patientId || '-')}</span></div>
                <div class="info-row"><span class="info-label">NRIC</span><span class="info-value">${escapeHtml(p.nric || '-')}</span></div>
                <div class="info-row"><span class="info-label">Phone</span><span class="info-value">${escapeHtml(p.phone || '-')}</span></div>
                <div class="info-row"><span class="info-label">Age</span><span class="info-value">${escapeHtml(String(p.age ?? '-'))}</span></div>
                <div class="info-row"><span class="info-label">Gender</span><span class="info-value">${escapeHtml(p.gender || '-')}</span></div>
                ${stalledPill}
                ${dischargePill}
            </div>
        `;
    }

    function renderAppointmentJourney(p) {
        if (!appointmentTypes.length) {
            return `
                <div class="journey-line">
                    <div class="journey-title">Appointment</div>
                    <div class="text-muted small">No appointment types configured.</div>
                </div>
            `;
        }

        const apptMap = appointmentsByPatient.get(p.patientId) || new Map();

        const cells = appointmentTypes.map(t => {
            const entry = apptMap.get(t.pjAppTypeId);
            const status = entry ? (entry.status || '') : 'Not Booked';
            const date = entry ? (entry.date || '') : '';
            const css = statusToCss(status);

            return `
                <div class="journey-cell ${css.cell}">
                    <div class="journey-dot"></div>
                    <div class="journey-card">
                        <div class="jc-name">${escapeHtml(t.pjAppTypeName)}</div>
                        <span class="jc-status ${css.pill}">${escapeHtml(status)}</span>
                        ${date ? `<span class="jc-date">${escapeHtml(date)}</span>` : ''}
                    </div>
                </div>
            `;
        }).join('');

        return `
            <div class="journey-line">
                <div class="journey-title">Appointment</div>
                <div class="journey-track">${cells}</div>
            </div>
        `;
    }

    function renderProcedureJourney(p) {
        const procs = proceduresByPatient.get(p.patientId) || [];

        if (procs.length === 0) {
            return `
                <div class="journey-line">
                    <div class="journey-title">Procedure</div>
                    <div class="text-muted small">No procedures recorded.</div>
                </div>
            `;
        }

        const css = statusToCss('Recorded');
        const cells = procs.map(pr => `
            <div class="journey-cell ${css.cell}">
                <div class="journey-dot"></div>
                <div class="journey-card">
                    <div class="jc-name">${escapeHtml(pr.pjAppTypeName)}</div>
                    <span class="jc-status ${css.pill}">Recorded</span>
                    ${pr.date ? `<span class="jc-date">${escapeHtml(pr.date)}</span>` : ''}
                </div>
            </div>
        `).join('');

        return `
            <div class="journey-line">
                <div class="journey-title">Procedure</div>
                <div class="journey-track">${cells}</div>
            </div>
        `;
    }

    // ---------- init ----------
    document.addEventListener('DOMContentLoaded', function () {
        searchInput = document.getElementById('trackerSearchInput');
        filterChipsEl = document.getElementById('trackerFilterChips');
        listEl = document.getElementById('trackerPatientList');
        stalledCountEl = document.getElementById('stalledCount');

        if (!listEl || !filterChipsEl || !searchInput || !stalledCountEl) return;

        searchInput.addEventListener('input', function () {
            searchTerm = (this.value || '').trim();
            renderPatientList();
        });

        loadTrackerData();
    });
})();
