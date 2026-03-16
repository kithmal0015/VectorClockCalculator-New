// SignalR Connection
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:8080/dashboardHub")
    .withAutomaticReconnect()
    .build();

let performanceChart;
let chartData = { success: [], failed: [], labels: [] };
const MAX_CHART_POINTS = 20;

// Initialize connection
connection.start()
    .then(() => {
        console.log('✅ Connected to dashboard');
        updateConnectionStatus(true);
        fetchSystemStatus();
        setInterval(fetchSystemStatus, 2000);
        initializeChart();
    })
    .catch(err => {
        console.error('❌ Connection failed:', err);
        updateConnectionStatus(false);
    });

// SignalR Event Handlers
connection.on("ReceiveSystemStatus", (status) => {
    updateDashboard(status);
});

connection.on("OperationCompleted", (operation) => {
    console.log('Operation completed:', operation);
    addOperationToHistory(operation);
    updateChartData(operation);
    fetchSystemStatus();
});

connection.on("ServerHealthUpdate", (servers) => {
    console.log('Health update:', servers);
});

connection.on("VectorClockUpdated", (serverId, clock) => {
    fetchSystemStatus();
});

connection.on("ServerStatusChanged", (serverId, isOnline, isPartitioned) => {
    fetchSystemStatus();
});

connection.on("HistoryCleared", () => {
    clearHistoryUI();
});

connection.onreconnected(() => {
    updateConnectionStatus(true);
    fetchSystemStatus();
});

connection.onclose(() => {
    updateConnectionStatus(false);
});

// Update connection status
function updateConnectionStatus(connected) {
    const statusDot = document.getElementById('statusDot');
    const statusText = document.getElementById('connectionStatus');
    
    if (connected) {
        statusDot.className = 'status-dot connected';
        statusText.textContent = '🟢 Connected';
    } else {
        statusDot.className = 'status-dot disconnected';
        statusText.textContent = '🔴 Disconnected';
    }
}

// Fetch system status
async function fetchSystemStatus() {
    try {
        const response = await fetch('http://localhost:8080/api/status');
        const data = await response.json();
        updateDashboard(data);
    } catch (err) {
        console.error('Error fetching status:', err);
    }
}

// Update dashboard with latest data
function updateDashboard(status) {
    document.getElementById('currentTime').textContent = 
        new Date(status.timestamp).toLocaleTimeString();

    document.getElementById('totalOps').textContent = status.statistics.totalOperations;
    document.getElementById('successOps').textContent = status.statistics.successfulOperations;
    document.getElementById('failedOps').textContent = status.statistics.failedOperations;
    document.getElementById('successRate').textContent = status.statistics.successRate.toFixed(1) + '%';

    document.getElementById('currentLeader').textContent = 
        status.leader ? `Leader: ${status.leader}` : 'Leader: None';

    updateServers(status.servers);

    if (status.recentOperations && status.recentOperations.length > 0) {
        updateHistory(status.recentOperations);
    }
}

// Update server displays
function updateServers(servers) {
    const container = document.getElementById('serversContainer');
    container.innerHTML = '';

    servers.forEach(server => {
        const serverCard = document.createElement('div');
        const statusClass = server.isPartitioned ? 'partitioned' : 
                           server.isOnline ? 'online' : 'offline';
        
        serverCard.className = `server-card ${statusClass}`;
        serverCard.setAttribute('data-server-id', server.serverId);
        
        const statusBadge = server.isPartitioned ? 'badge-partitioned' : 
                           server.isOnline ? 'badge-online' : 'badge-offline';
        const statusText = server.isPartitioned ? 'Partitioned' : 
                          server.isOnline ? 'Online' : 'Offline';

        const leaderBadge = server.isLeader ? 
            '<span class="badge badge-leader">Main</span>' : '';

        const clockStr = formatVectorClock(server.vectorClock);

        const actionButton = server.isPartitioned || !server.isOnline ?
            `<button onclick="reconnectServer('${server.serverId}')" style="width: 100%; margin-top: 10px; padding: 8px; font-size: 0.85rem;">🔄 Reconnect</button>` :
            `<button onclick="partitionServer('${server.serverId}')" class="danger" style="width: 100%; margin-top: 10px; padding: 8px; font-size: 0.85rem;">🔴 Partition</button>`;

        serverCard.innerHTML = `
            <div class="server-header">
                <div class="server-name">
                    ${server.serverId}
                </div>
                <div>
                    ${leaderBadge}
                    <span class="badge ${statusBadge}">${statusText}</span>
                </div>
            </div>
            <div class="server-stats">
                <div class="stat">
                    <span class="stat-value">${server.totalOperations}</span>
                    <span class="stat-label">Total Ops</span>
                </div>
                <div class="stat">
                    <span class="stat-value" style="color: #10b981">${server.successfulOperations}</span>
                    <span class="stat-label">Success</span>
                </div>
                <div class="stat">
                    <span class="stat-value" style="color: #ef4444">${server.failedOperations}</span>
                    <span class="stat-label">Failed</span>
                </div>
                <div class="stat">
                    <span class="stat-value">${server.successRate.toFixed(0)}%</span>
                    <span class="stat-label">Rate</span>
                </div>
            </div>
            <div class="vector-clock">
                <strong>Vector Clock:</strong><br>
                ${clockStr}
            </div>
            ${actionButton}
        `;

        container.appendChild(serverCard);
    });
}

// Format vector clock for display
function formatVectorClock(clock) {
    if (!clock || Object.keys(clock).length === 0) {
        return '{ empty }';
    }
    return '{ ' + Object.entries(clock)
        .map(([key, value]) => `${key}: ${value}`)
        .join(', ') + ' }';
}

// Update operation history
function updateHistory(operations) {
    const tbody = document.getElementById('historyBody');
    tbody.innerHTML = '';

    if (operations.length === 0) {
        tbody.innerHTML = `
            <tr>
                <td colspan="6">
                    <div class="empty-state">
                        <div>No operations yet. Perform a calculation above.</div>
                    </div>
                </td>
            </tr>
        `;
        return;
    }

    operations.forEach(op => {
        const row = document.createElement('tr');
        const time = new Date(op.timestamp).toLocaleTimeString();
        const resultClass = op.success ? 'result-success' : 'result-failed';
        
        let inputStr = op.operation === 'Multiply' || op.operation === 'SlowMultiply' ? 
            `${op.input1} × ${op.input2}` : op.input1;

        // Check if it was a failover
        const isFailover = op.errorMessage && op.errorMessage.includes('Failover');
        const failoverBadge = isFailover ? '<span class="retry-badge" style="background: #3b82f6;">Failover</span>' : '';

        row.innerHTML = `
            <td>${time}</td>
            <td>${op.clientId}</td>
            <td>${op.serverId} ${failoverBadge}</td>
            <td>${op.operation}</td>
            <td>${inputStr}</td>
            <td class="${resultClass}">${op.success ? op.result.toFixed(2) : 'Failed'}</td>
        `;

        tbody.appendChild(row);
    });
}

// Add single operation to history
function addOperationToHistory(operation) {
    const tbody = document.getElementById('historyBody');
    
    const emptyState = tbody.querySelector('.empty-state');
    if (emptyState) {
        tbody.innerHTML = '';
    }

    const row = document.createElement('tr');
    const time = new Date(operation.timestamp).toLocaleTimeString();
    const resultClass = operation.success ? 'result-success' : 'result-failed';

    let inputStr = operation.operation === 'Multiply' || operation.operation === 'SlowMultiply' ? 
        `${operation.input1} × ${operation.input2}` : operation.input1;

    // Check if it was a failover
    const isFailover = operation.errorMessage && operation.errorMessage.includes('Failover');
    const failoverBadge = isFailover ? '<span class="retry-badge" style="background: #3b82f6;">Failover</span>' : '';

    row.innerHTML = `
        <td>${time}</td>
        <td>${operation.clientId}</td>
        <td>${operation.serverId} ${failoverBadge}</td>
        <td>${operation.operation}</td>
        <td>${inputStr}</td>
        <td class="${resultClass}">${operation.success ? operation.result.toFixed(2) : 'Failed'}</td>
    `;

    tbody.insertBefore(row, tbody.firstChild);

    // Highlight server if failover
    if (isFailover) {
        highlightFailoverServer(operation.serverId);
    }

    while (tbody.children.length > 20) {
        tbody.removeChild(tbody.lastChild);
    }
}

// Highlight the server that handled failover
function highlightFailoverServer(serverId) {
    const serverCards = document.querySelectorAll('.server-card');
    serverCards.forEach(card => {
        if (card.getAttribute('data-server-id') === serverId) {
            card.style.animation = 'pulse-green 1s ease 3';
            setTimeout(() => {
                card.style.animation = '';
            }, 3000);
        }
    });
}

// Clear history UI
function clearHistoryUI() {
    const tbody = document.getElementById('historyBody');
    tbody.innerHTML = `
        <tr>
            <td colspan="6">
                <div class="empty-state">
                    <div>History cleared. Perform a calculation to see new operations.</div>
                </div>
            </td>
        </tr>
    `;
    
    chartData = { success: [], failed: [], labels: [] };
    updateChart();
}

// Initialize chart
function initializeChart() {
    const ctx = document.getElementById('performanceChart');
    
    performanceChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: chartData.labels,
            datasets: [
                {
                    label: 'Successful Operations',
                    data: chartData.success,
                    borderColor: '#10b981',
                    backgroundColor: 'rgba(16, 185, 129, 0.1)',
                    tension: 0.4,
                    fill: true
                },
                {
                    label: 'Failed Operations',
                    data: chartData.failed,
                    borderColor: '#ef4444',
                    backgroundColor: 'rgba(239, 68, 68, 0.1)',
                    tension: 0.4,
                    fill: true
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    labels: { color: '#e2e8f0' }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: { color: '#94a3b8' },
                    grid: { color: '#334155' }
                },
                x: {
                    ticks: { color: '#94a3b8' },
                    grid: { color: '#334155' }
                }
            }
        }
    });
}

// Update chart data
function updateChartData(operation) {
    const time = new Date(operation.timestamp).toLocaleTimeString();
    
    chartData.labels.push(time);
    const lastSuccess = chartData.success.length > 0 ? 
        chartData.success[chartData.success.length - 1] : 0;
    const lastFailed = chartData.failed.length > 0 ? 
        chartData.failed[chartData.failed.length - 1] : 0;
    
    chartData.success.push(lastSuccess + (operation.success ? 1 : 0));
    chartData.failed.push(lastFailed + (!operation.success ? 1 : 0));

    if (chartData.labels.length > MAX_CHART_POINTS) {
        chartData.labels.shift();
        chartData.success.shift();
        chartData.failed.shift();
    }

    updateChart();
}

// Update chart display
function updateChart() {
    if (performanceChart) {
        performanceChart.data.labels = chartData.labels;
        performanceChart.data.datasets[0].data = chartData.success;
        performanceChart.data.datasets[1].data = chartData.failed;
        performanceChart.update('none');
    }
}

// Handle operation select change
document.getElementById('operationSelect').addEventListener('change', (e) => {
    const number2Row = document.getElementById('number2Row');
    if (e.target.value === 'multiply') {
        number2Row.style.display = 'grid';
    } else {
        number2Row.style.display = 'none';
    }
});

// Perform calculation - WITH AUTOMATIC FAILOVER
async function performCalculation() {
    const serverUrl = document.getElementById('serverSelect').value;
    const operation = document.getElementById('operationSelect').value;
    const number1 = parseFloat(document.getElementById('number1').value);
    const number2 = parseFloat(document.getElementById('number2').value);

    if (isNaN(number1)) {
        alert('❌ Please enter a valid number');
        return;
    }

    if (operation === 'multiply' && isNaN(number2)) {
        alert('❌ Please enter a valid second number for multiplication');
        return;
    }

    const calcBtn = document.getElementById('calcBtn');
    const originalText = calcBtn.textContent;
    calcBtn.disabled = true;
    calcBtn.textContent = 'Processing...';

    try {
        let response;
        
        if (serverUrl === 'auto') {
            // Auto mode with failover
            calcBtn.textContent = 'Auto-Failover Active...';
            response = await fetch('http://localhost:8080/api/calculate-auto', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    operation: operation,
                    number1: number1,
                    number2: number2 || 0
                })
            });
        } else {
            // Specific server with automatic failover on failure
            response = await fetch('http://localhost:8080/api/calculate', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    serverUrl: serverUrl,
                    operation: operation,
                    number1: number1,
                    number2: number2 || 0
                })
            });
        }

        const result = await response.json();
        
        if (result.success) {
            // Check if it was a failover
            const isFailover = result.errorMessage && result.errorMessage.includes('Failover');
            const title = isFailover ? '✅ Success (Auto-Failover!)' : '✅ Success!';
            const message = isFailover 
                ? `Result: ${result.result.toFixed(2)}\n${result.errorMessage}` 
                : `Result: ${result.result.toFixed(2)} (Server: ${result.serverId})`;
            
            showNotification(title, message, 'success');
            
            // Show failover animation if it happened
            if (isFailover) {
                highlightFailoverServer(result.serverId);
            }
        } else {
            showNotification('❌ All Servers Failed', result.errorMessage, 'error');
        }

        document.getElementById('number1').value = '';
        document.getElementById('number2').value = '';
        
    } catch (err) {
        console.error('Calculation error:', err);
        showNotification('❌ Error', 'Failed to perform calculation: ' + err.message, 'error');
    } finally {
        calcBtn.disabled = false;
        calcBtn.textContent = originalText;
    }
}

// Show notification
function showNotification(title, message, type) {
    const notification = document.createElement('div');
    notification.style.cssText = `
        position: fixed;
        top: 80px;
        right: 20px;
        background: ${type === 'success' ? '#10b981' : '#ef4444'};
        color: white;
        padding: 15px 20px;
        border-radius: 8px;
        box-shadow: 0 4px 12px rgba(0,0,0,0.3);
        z-index: 10000;
        animation: slideIn 0.3s ease;
        min-width: 300px;
        max-width: 400px;
    `;
    
    notification.innerHTML = `
        <div style="font-weight: 600; margin-bottom: 5px;">${title}</div>
        <div style="font-size: 0.9rem; white-space: pre-line;">${message}</div>
    `;
    
    document.body.appendChild(notification);
    
    setTimeout(() => {
        notification.style.animation = 'slideOut 0.3s ease';
        setTimeout(() => notification.remove(), 300);
    }, 4000);
}

// Add CSS animations
const style = document.createElement('style');
style.textContent = `
    @keyframes slideIn {
        from {
            transform: translateX(400px);
            opacity: 0;
        }
        to {
            transform: translateX(0);
            opacity: 1;
        }
    }
    @keyframes slideOut {
        from {
            transform: translateX(0);
            opacity: 1;
        }
        to {
            transform: translateX(400px);
            opacity: 0;
        }
    }
    @keyframes pulse-green {
        0%, 100% {
            box-shadow: 0 0 0 0 rgba(16, 185, 129, 0.7);
            transform: scale(1);
        }
        50% {
            box-shadow: 0 0 20px 10px rgba(16, 185, 129, 0);
            transform: scale(1.05);
        }
    }
`;
document.head.appendChild(style);

// Partition server
async function partitionServer(serverId) {
    if (!confirm(`Are you sure you want to partition ${serverId}?\n\nThis will make it unavailable and operations will automatically failover to other servers.`)) {
        return;
    }

    try {
        await fetch(`http://localhost:8080/api/partition/${serverId}`, {
            method: 'POST'
        });
        showNotification('🔴 Server Partitioned', `${serverId} is now disconnected. Operations will automatically failover.`, 'error');
        fetchSystemStatus();
    } catch (err) {
        console.error('Partition error:', err);
        showNotification('❌ Error', 'Failed to partition server', 'error');
    }
}

// Reconnect server
async function reconnectServer(serverId) {
    try {
        await fetch(`http://localhost:8080/api/reconnect/${serverId}`, {
            method: 'POST'
        });
        showNotification('🟢 Server Reconnected', `${serverId} is back online and ready to handle requests.`, 'success');
        fetchSystemStatus();
    } catch (err) {
        console.error('Reconnect error:', err);
        showNotification('❌ Error', 'Failed to reconnect server', 'error');
    }
}

// Clear history
async function clearHistory() {
    if (!confirm('Are you sure you want to clear the operation history?')) {
        return;
    }

    try {
        await fetch('http://localhost:8080/api/clear-history', {
            method: 'POST'
        });
        clearHistoryUI();
        showNotification('History Cleared', 'Operation history has been cleared successfully.', 'success');
    } catch (err) {
        console.error('Error clearing history:', err);
        showNotification('❌ Error', 'Failed to clear history', 'error');
    }
}

// Auto-refresh indicator
setInterval(() => {
    const time = document.getElementById('currentTime');
    if (time) {
        time.style.opacity = '0.5';
        setTimeout(() => time.style.opacity = '1', 200);
    }
}, 2000);

console.log('✅ Dashboard initialized with automatic failover support');