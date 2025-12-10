// @ts-nocheck
(function() {
    const baseUrl = '/Dashboard';

    let msgEl;

    let chartRace = null;
    let chartAge = null;
    let chartDischargeType = null;
    let chartAdmitDischarge = null;

    // ---------- helpers ----------

    function setKpiText(elementId, value) {
        const el = document.getElementById(elementId);
        if (!el) return;
        el.textContent = (value !== null && value !== undefined) ? value : '-';
    }

    async function getJson(url) {
        const response = await fetch(url, {
            method: 'GET',
            headers: { 'Accept': 'application/json' }
        });

        if (!response.ok) {
            throw new Error('HTTP error ' + response.status);
        }

        return await response.json();
    }

    function setBranchesOnSelect(selectEl, branches) {
    if (!selectEl) return;

    selectEl.innerHTML = '';

    const optAll = document.createElement('option');
    optAll.value = '';
    optAll.textContent = 'All Branches';
    selectEl.appendChild(optAll);

    (branches || []).forEach(b => {
        const opt = document.createElement('option');
        // Use branchName as the value because the API filters by Branch_Name
        opt.value = b.branchName || '';
        opt.textContent = b.branchName || '';
        // Optional: still keep ID if you ever need it later
        opt.dataset.branchId = b.branchId || '';
        selectEl.appendChild(opt);
    });
}

    // ---------- row 1: KPIs ----------

    async function loadActiveBranchCount() {
        try {
            const result = await getJson(`${baseUrl}/GetActiveBranchCount`);
            if (!result.success) throw new Error(result.message || 'Error');
            setKpiText('kpiActiveBranches', result.count || 0);
        } catch (err) {
            console.error('Error loading active branch count', err);
            setKpiText('kpiActiveBranches', '-');
        }
    }

    async function loadBranchesForKpiCards() {
        try {
            const result = await getJson(`${baseUrl}/GetDashboardBranches`);
            if (!result.success) throw new Error(result.message || 'Error');

            const branches = result.data || [];
            const ddlActive = document.getElementById('ddlActivePatientsBranch');
            const ddlDischarged = document.getElementById('ddlDischargedPatientsBranch');

            setBranchesOnSelect(ddlActive, branches);
            setBranchesOnSelect(ddlDischarged, branches);
        } catch (err) {
            console.error('Error loading branches', err);
        }
    }

    async function loadActivePatientCount(branchId) {
    try {
        const url = branchId
            ? `${baseUrl}/GetActivePatientsCount?branchName=${encodeURIComponent(branchId)}`
            : `${baseUrl}/GetActivePatientsCount`;

        const result = await getJson(url);
        if (!result.success) throw new Error(result.message || 'Error');

        setKpiText('kpiActivePatients', result.count || 0);
    } catch (err) {
        console.error('Error loading active patient count', err);
        setKpiText('kpiActivePatients', '-');
    }
}

    async function loadDischargedPatientCount(branchId) {
    try {
        const url = branchId
            ? `${baseUrl}/GetDischargedPatientsCount?branchName=${encodeURIComponent(branchId)}`
            : `${baseUrl}/GetDischargedPatientsCount`;

        const result = await getJson(url);
        if (!result.success) throw new Error(result.message || 'Error');

        setKpiText('kpiDischargedPatients', result.count || 0);
    } catch (err) {
        console.error('Error loading discharged patient count', err);
        setKpiText('kpiDischargedPatients', '-');
    }
}

    // ---------- row 2: pie charts ----------

    // small colour palette for race – will cycle if there are many races
    const raceColours = [
        '#4e79a7', '#f28e2b', '#e15759', '#76b7b2',
        '#59a14f', '#edc948', '#b07aa1', '#ff9da7',
        '#9c755f', '#bab0ab'
    ];

    async function loadChartPatientsByRace() {
        try {
            const result = await getJson(`${baseUrl}/GetPatientsByRace`);
            if (!result.success) throw new Error(result.message || 'Error');

            const list = result.data || [];
            const labels = list.map(x => x.label || '');
            const values = list.map(x => x.count || 0);
            const colours = labels.map((_, idx) => raceColours[idx % raceColours.length]);

            const ctx = document.getElementById('chartPatientsByRace');
            if (!ctx) return;

            if (chartRace) chartRace.destroy();

            chartRace = new Chart(ctx, {
                type: 'pie',
                data: {
                    labels: labels,
                    datasets: [{
                        data: values,
                        backgroundColor: colours
                    }]
                },
                options: {
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            position: 'bottom'
                        }
                    }
                }
            });
        } catch (err) {
            console.error('Error loading Patients by Race chart', err);
        }
    }

    async function loadChartPatientsByAgeGroup() {
        try {
            const result = await getJson(`${baseUrl}/GetPatientsByAgeGroup`);
            if (!result.success) throw new Error(result.message || 'Error');

            const list = result.data || [];
            const labels = list.map(x => x.label || '');
            const values = list.map(x => x.count || 0);

            // fixed palette (max 5 main age buckets)
            const ageColours = [
                '#4e79a7',
                '#f28e2b',
                '#e15759',
                '#76b7b2',
                '#59a14f'
            ];

            const ctx = document.getElementById('chartPatientsByAgeGroup');
            if (!ctx) return;

            if (chartAge) chartAge.destroy();

            chartAge = new Chart(ctx, {
                type: 'pie',
                data: {
                    labels: labels,
                    datasets: [{
                        data: values,
                        backgroundColor: labels.map((_, i) => ageColours[i % ageColours.length])
                    }]
                },
                options: {
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            position: 'bottom'
                        }
                    }
                }
            });
        } catch (err) {
            console.error('Error loading Patients by Age Group chart', err);
        }
    }

    // ---------- row 3: discharge type bar chart (colour #826ccb) ----------

    async function loadChartPatientsByDischargeType() {
        try {
            const result = await getJson(`${baseUrl}/GetPatientsByDischargeType`);
            if (!result.success) throw new Error(result.message || 'Error');

            const list = result.data || [];
            const labels = list.map(x => x.label || '');
            const values = list.map(x => x.count || 0);

            const ctx = document.getElementById('chartPatientsByDischargeType');
            if (!ctx) return;

            if (chartDischargeType) chartDischargeType.destroy();

            chartDischargeType = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Patients',
                        data: values,
                        backgroundColor: '#826ccb'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        x: {
                            ticks: { autoSkip: false }
                        },
                        y: {
                            beginAtZero: true
                        }
                    }
                }
            });
        } catch (err) {
            console.error('Error loading Patients by Discharge Type chart', err);
        }
    }

    // ---------- row 4: horizontal diverging bar (Admissions vs Discharges) ----------

    async function loadChartAdmitDischarge() {
        try {
            const result = await getJson(`${baseUrl}/GetAdmitDischargeLast12Months`);
            if (!result.success) throw new Error(result.message || 'Error');

            const list = result.data || [];
            const labels = list.map(x => x.label || '');
            const admitted = list.map(x => -(x.admittedCount || 0));    // negative => left side
            const discharged = list.map(x => x.dischargedCount || 0);   // positive => right side

            const ctx = document.getElementById('chartAdmitDischarge');
            if (!ctx) return;

            if (chartAdmitDischarge) chartAdmitDischarge.destroy();

            chartAdmitDischarge = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [
                        {
                            label: 'Admitted',
                            data: admitted,
                            backgroundColor: '#0000ff'
                        },
                        {
                            label: 'Discharged',
                            data: discharged,
                            backgroundColor: '#f0a207'
                        }
                    ]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        x: {
                            stacked: true,
                            ticks: {
                                callback: function(value) {
                                    // show absolute numbers on axis
                                    return Math.abs(value);
                                }
                            }
                        },
                        y: {
                            stacked: true
                        }
                    },
                    plugins: {
                        tooltip: {
                            callbacks: {
                                label: function(ctx) {
                                    const label = ctx.dataset.label || '';
                                    const val = ctx.parsed.x || 0;
                                    return `${label}: ${Math.abs(val)}`;
                                }
                            }
                        }
                    }
                }
            });
        } catch (err) {
            console.error('Error loading Admissions vs Discharges chart', err);
        }
    }

    // ---------- init ----------

    document.addEventListener('DOMContentLoaded', async function() {
        msgEl = document.getElementById('dashboardMessage');
        if (msgEl) {
            msgEl.textContent = '';
            msgEl.classList.remove('text-danger', 'text-success');
        }

        // 1) KPI stuff
        await loadBranchesForKpiCards();
        await loadActiveBranchCount();
        await loadActivePatientCount(null);
        await loadDischargedPatientCount(null);

        const ddlActive = document.getElementById('ddlActivePatientsBranch');
        if (ddlActive) {
            ddlActive.addEventListener('change', function() {
                const branchId = ddlActive.value || null;
                loadActivePatientCount(branchId);
            });
        }

        const ddlDischarged = document.getElementById('ddlDischargedPatientsBranch');
        if (ddlDischarged) {
            ddlDischarged.addEventListener('change', function() {
                const branchId = ddlDischarged.value || null;
                loadDischargedPatientCount(branchId);
            });
        }

        // 2) Charts
        loadChartPatientsByRace();
        loadChartPatientsByAgeGroup();
        loadChartPatientsByDischargeType();
        loadChartAdmitDischarge();
    });
})();