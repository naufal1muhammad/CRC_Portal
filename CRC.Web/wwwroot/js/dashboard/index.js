// @ts-nocheck
(function () {
    //PATIENTS BY STAGE BAR GRAPH
    const branchesEl = document.getElementById('activeBranchesCount');
    const canvas = document.getElementById('patientsStageChartCanvas');

    if (!branchesEl || !canvas) return;

    async function loadDashboardSummary() {
        try {
            const response = await fetch('/Dashboard/GetSummary', {
                method: 'GET',
                headers: {
                    'Accept': 'application/json'
                }
            });

            if (!response.ok) {
                console.error('Failed to load dashboard summary.');
                return;
            }

            const data = await response.json();

            // Update active branches
            branchesEl.textContent = data.activeBranchesCount ?? 0;

            // Build chart
            const ctx = canvas.getContext('2d');

            const chartData = {
                labels: ['T2', 'T3', 'T4', 'T5'],
                datasets: [{
                    label: 'Patients',
                    data: [
                        data.t2 ?? 0,
                        data.t3 ?? 0,
                        data.t4 ?? 0,
                        data.t5 ?? 0
                    ],
                    backgroundColor: '#826ccb',
                    borderColor: '#5f4aa3',
                    borderWidth: 1
                }]
            };

            const chartConfig = {
                type: 'bar',
                data: chartData,
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            display: false
                        },
                        title: {
                            display: false
                        }
                    },
                    scales: {
                        y: {
                            beginAtZero: true,
                            precision: 0
                        }
                    }
                }
            };

            // Create chart
            new Chart(ctx, chartConfig);

        } catch (err) {
            console.error('Error loading dashboard summary:', err);
        }
    }

    // Load on page load
    loadDashboardSummary();

    //PATIENT BY STATE PIE CHART
    let stateStagePieChart = null;

    const stateSelect = document.getElementById('ddlPatientStateFilter');
    const emptyMessage = document.getElementById('stateStagePieChartEmpty');
    const chartCanvas = document.getElementById('stateStagePieChart');

    async function loadPatientStates() {
        if (!stateSelect) return;

        try {
            const response = await fetch('/Dashboard/GetPatientStates', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                console.error('Failed to load patient states.');
                return;
            }

            const states = await response.json(); // ["Johor","Selangor",...]

            // Clear existing options except "All States"
            while (stateSelect.options.length > 1) {
                stateSelect.remove(1);
            }

            states.forEach(s => {
                const opt = document.createElement('option');
                opt.value = s;
                opt.textContent = s;
                stateSelect.appendChild(opt);
            });
        } catch (err) {
            console.error('Error loading patient states', err);
        }
    }

    async function loadStateStagePieChart(selectedState) {
        if (!chartCanvas) return;

        try {
            const url = '/Dashboard/GetPatientStageCountsByState?state=' + encodeURIComponent(selectedState || '');
            const response = await fetch(url, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });

            if (!response.ok) {
                console.error('Failed to load stage counts for state.');
                return;
            }

            const result = await response.json();
            if (!result.success) {
                console.error('API returned success = false');
                return;
            }

            const data = result.data || {};
            const values = [
                data.t2 || 0,
                data.t3 || 0,
                data.t4 || 0,
                data.t5 || 0
            ];

            const total = values.reduce((sum, v) => sum + v, 0);

            if (emptyMessage) {
                if (total === 0) {
                    emptyMessage.classList.remove('d-none');
                } else {
                    emptyMessage.classList.add('d-none');
                }
            }

            const labels = ['T2 (Screening)', 'T3 (Diagnosis)', 'T4 (Staging)', 'T5 (Treatment)'];
            const stageColors = ['#4e79a7', '#f28e2c', '#e15759', '#76b7b2'];

            const chartData = {
                labels: labels,
                datasets: [{
                    label: 'Patients',
                    data: values,
                    backgroundColor: stageColors,
                    borderColor: '#ffffff',
                    borderWidth: 1,
                }]
            };

            
            const config = {
                type: 'pie',
                data: chartData,
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            position: 'bottom'
                        },
                        tooltip: {
                            callbacks: {
                                label: function (context) {
                                    const label = context.label || '';
                                    const value = context.parsed || 0;
                                    return `${label}: ${value}`;
                                }
                            }
                        }
                    }
                }
            };

            if (stateStagePieChart) {
                // Update existing chart
                stateStagePieChart.data = chartData;
                stateStagePieChart.update();
            } else {
                // Create new chart
                stateStagePieChart = new Chart(chartCanvas.getContext('2d'), config);
            }
        } catch (err) {
            console.error('Error loading state stage pie chart', err);
        }
    }

    function attachEvents() {
        if (!stateSelect) return;

        stateSelect.addEventListener('change', function () {
            const selected = stateSelect.value;
            loadStateStagePieChart(selected);
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        loadPatientStates().then(() => {
            // After states are loaded, show chart for "All States" by default
            loadStateStagePieChart('');
        });

        attachEvents();
    });
})();