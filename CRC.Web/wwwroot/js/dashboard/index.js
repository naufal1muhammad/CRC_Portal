// @ts-nocheck
(function () {
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
                    ]
                }]
            };

            const chartConfig = {
                type: 'bar',
                data: chartData,
                options: {
                    responsive: true,
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
})();