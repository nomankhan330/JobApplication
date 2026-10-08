// Dashboard dynamic charts: fetch Sales vs Purchases trend and render area chart
(function () {
    function renderAreaChart(labels, sales, purchases) {
        var options = {
            series: [
                { name: 'Sales', data: sales },
                { name: 'Purchases', data: purchases }
            ],
            chart: { toolbar: { show: false }, height: 350, type: 'area' },
            dataLabels: { enabled: false },
            yaxis: { labels: { formatter: function (e) { return e; } }, tickAmount: 4, min: 0 },
            stroke: { curve: 'smooth', width: 2 },
            grid: { show: true, borderColor: '#90A4AE', strokeDashArray: 0, position: 'back', xaxis: { lines: { show: true } }, yaxis: { lines: { show: true } }, padding: { top: 10, right: 0, bottom: 10, left: 10 } },
            legend: { show: true },
            colors: ['#0f9cf3', '#6fd088'],
            labels: labels
        };

        var chartEl = document.querySelector('#area_chart');
        if (!chartEl) return;

        // destroy previous instance if exists
        if (chartEl._apexChart) {
            try { chartEl._apexChart.destroy(); } catch (e) { }
        }

        var chart = new ApexCharts(chartEl, options);
        chart.render();
        chartEl._apexChart = chart;
    }

    function formatCurrency(amount) {
        return 'Rs. ' + Number(amount || 0).toLocaleString('en-PK', { minimumFractionDigits: 0 });
    }

    function fetchTrend(period, from, to, month) {
        var data = {};
        if (month) {
            data.month = month;
        } else {
            data.period = period || 'ThisYear';
        }
        if (from) data.from = from;
        if (to) data.to = to;

        $.getJSON('/Dashboard/GetSalesPurchaseTrend', data)
            .done(function (resp) {
                if (resp && !resp.error) {
                    renderAreaChart(resp.labels || [], resp.sales || [], resp.purchases || []);

                    // Update KPI elements with range totals if present
                    if (typeof resp.salesRangeTotal !== 'undefined') {
                        $('#salesWeekAmount').text(formatCurrency(resp.salesRangeTotal));
                    }
                    if (typeof resp.purchasesRangeTotal !== 'undefined') {
                        $('#purchasesWeekAmount').text(formatCurrency(resp.purchasesRangeTotal));
                    }
                    if (typeof resp.netRange !== 'undefined') {
                        $('#netMarginAmount').text(formatCurrency(resp.netRange));
                    }
                } else {
                    console.error('Failed to load trend:', resp && resp.message);
                }
            })
            .fail(function (xhr, status, err) {
                console.error('AJAX error fetching trend:', err);
            });
    }

    // Revenue Billing vs Cash Collection chart
    function renderColumnLineChart(labels, invoiced, collected) {
        var options = {
            series: [
                { name: 'Invoiced', type: 'column', data: invoiced },
                { name: 'Collected', type: 'line', data: collected }
            ],
            chart: { height: 350, type: 'line', toolbar: { show: false } },
            stroke: { width: [0, 2.5], curve: 'straight' },
            plotOptions: { bar: { horizontal: false, columnWidth: '34%' } },
            dataLabels: { enabled: false },
            markers: { size: [0, 3.5] },
            legend: { show: false },
            yaxis: { labels: { formatter: function (e) { return e; } }, tickAmount: 5, min: 0 },
            colors: ['#0f9cf3', '#6fd088'],
            labels: labels
        };

        var chartEl = document.querySelector('#column_line_chart');
        if (!chartEl) return;

        if (chartEl._apexChart) {
            try { chartEl._apexChart.destroy(); } catch (e) { }
        }

        var chart = new ApexCharts(chartEl, options);
        chart.render();
        chartEl._apexChart = chart;
    }

    function fetchRevenue(period, from, to, month) {
        var data = {};
        if (month) {
            data.month = month;
        } else {
            data.period = period || 'ThisYear';
        }
        if (from) data.from = from;
        if (to) data.to = to;

        $.getJSON('/Dashboard/GetRevenueBillingTrend', data)
            .done(function (resp) {
                if (resp && !resp.error) {
                    renderColumnLineChart(resp.labels || [], resp.invoiced || [], resp.collected || []);

                    if (typeof resp.totalInvoiced !== 'undefined') {
                        $('#totalInvoicedAmount').text(formatCurrency(resp.totalInvoiced));
                    }
                    if (typeof resp.totalCollected !== 'undefined') {
                        $('#collectedCashAmount').text(formatCurrency(resp.totalCollected));
                    }
                    if (typeof resp.remaining !== 'undefined') {
                        $('#remainingRecoveryAmount').text(formatCurrency(resp.remaining));
                    }
                } else {
                    console.error('Failed to load revenue:', resp && resp.message);
                }
            })
            .fail(function (xhr, status, err) {
                console.error('AJAX error fetching revenue:', err);
            });
    }

    // initial load (Admin dashboard month dropdown takes precedence when present)
    (function () {
        var month = $('#dashboardMonth').length ? $('#dashboardMonth').val() : null;
        if (month) {
            fetchTrend(null, null, null, month);
            fetchRevenue(null, null, null, month);
        } else {
            fetchTrend('ThisYear');
            fetchRevenue('ThisYear');
        }
    })();

    // Apply / Clear date range handlers
    $(document).on('click', '#applyDateRange', function (e) {
        e.preventDefault();
        var from = $('#globalFromDate').val();
        var to = $('#globalToDate').val();
        if (!from || !to) {
            alert('Please select both From and To dates');
            return;
        }

        // call endpoints with explicit from/to (format yyyy-MM-dd)
        DashboardFetchRevenue(null, from, to);
        DashboardFetchTrend(null, from, to);
    });

    $(document).on('click', '#clearDateRange', function (e) {
        e.preventDefault();
        $('#globalFromDate').val('');
        $('#globalToDate').val('');
        // reload defaults
        DashboardFetchRevenue('ThisYear');
        DashboardFetchTrend('ThisYear');
    });

    // expose for UI controls if needed
    window.DashboardFetchTrend = fetchTrend;
    window.DashboardFetchRevenue = fetchRevenue;

    // Dropdown handler for revenue period (Current Year / Last Year)
    $(document).on('click', '.revenue-period', function (e) {
        e.preventDefault();
        var period = $(this).data('period') || 'ThisYear';
        var label = $(this).text().trim();
        // update visible label keeping the chevron icon
        $('#revenuePeriodLabel').html(label + ' <i class="mdi mdi-chevron-down ms-1"></i>');

        // reload both charts/kpis for the selected period
        DashboardFetchRevenue(period);
        DashboardFetchTrend(period);
    });
})();
