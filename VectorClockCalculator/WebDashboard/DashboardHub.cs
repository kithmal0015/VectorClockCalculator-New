using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebDashboard
{
    public class DashboardHub : Hub
    {
        private readonly DashboardDataService _dataService;

        public DashboardHub(DashboardDataService dataService)
        {
            _dataService = dataService;
        }

        public async Task GetSystemStatus()
        {
            var status = _dataService.GetSystemStatus();
            await Clients.Caller.SendAsync("ReceiveSystemStatus", status);
        }

        public async Task GetOperationHistory(int count = 50)
        {
            var history = _dataService.GetOperationHistory(count);
            await Clients.Caller.SendAsync("ReceiveOperationHistory", history);
        }

        public async Task UpdateVectorClock(string serverId, Dictionary<string, int> clock)
        {
            _dataService.UpdateVectorClock(serverId, clock);
            await Clients.All.SendAsync("VectorClockUpdated", serverId, clock);
        }

        public async Task ReportOperation(OperationHistory operation)
        {
            _dataService.AddOperation(operation);
            await Clients.All.SendAsync("OperationCompleted", operation);
        }

        public async Task UpdateServerStatus(string serverId, bool isOnline, bool isPartitioned)
        {
            _dataService.UpdateServerStatus(serverId, isOnline, isPartitioned);
            await Clients.All.SendAsync("ServerStatusChanged", serverId, isOnline, isPartitioned);
        }

        public async Task ClearHistory()
        {
            _dataService.ClearHistory();
            await Clients.All.SendAsync("HistoryCleared");
        }
    }
}