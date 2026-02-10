// @ts-nocheck
(function() {
    const baseUrl = '/Dashboard';

    let msgEl;

    let chartRace = null;
    let chartAge = null;
    let chartDischargeType = null;

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

    // ---------- init ----------

    document.addEventListener('DOMContentLoaded', async function() {
        msgEl = document.getElementById('dashboardMessage');
        if (msgEl) {
            msgEl.textContent = '';
            msgEl.classList.remove('text-danger', 'text-success');
        }

        // 1) KPI stuff
        await loadActiveBranchCount();

        // 2) Charts
        loadChartPatientsByRace();
        loadChartPatientsByAgeGroup();
        loadChartPatientsByDischargeType();
    });
})();
