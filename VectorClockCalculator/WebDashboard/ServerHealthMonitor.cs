using Grpc.Net.Client;
using Microsoft.AspNetCore.SignalR;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WebDashboard
{
    public class ServerHealthMonitor
    {
        private readonly DashboardDataService _dataService;
        private readonly IHubContext<DashboardHub> _hubContext;
        private CancellationTokenSource? _cts;
        private readonly List<ServerEndpoint> _servers;

        public class ServerEndpoint
        {
            public string ServerId { get; set; } = "";
            public string Url { get; set; } = "";
            public bool IsHealthy { get; set; } = false;
            public DateTime LastCheck { get; set; }
        }

        public ServerHealthMonitor(
            DashboardDataService dataService,
            IHubContext<DashboardHub> hubContext)
        {
            _dataService = dataService;
            _hubContext = hubContext;
            _servers = new List<ServerEndpoint>
            {
                new ServerEndpoint { ServerId = "Server1", Url = "http://localhost:5000" },
                new ServerEndpoint { ServerId = "Server2", Url = "http://localhost:5001" },
                new ServerEndpoint { ServerId = "Server3", Url = "http://localhost:5002" }
            };
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => MonitorLoop(_cts.Token));
            Console.WriteLine("Server health monitor started");
        }

        public void Stop()
        {
            _cts?.Cancel();
            Console.WriteLine("Server health monitor stopped");
        }

        private async Task MonitorLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var server in _servers)
                    {
                        await CheckServerHealth(server);
                    }

                    // Broadcast status to all connected clients
                    await _hubContext.Clients.All.SendAsync("ServerHealthUpdate", 
                        _servers.Select(s => new 
                        { 
                            s.ServerId, 
                            s.IsHealthy, 
                            LastCheck = s.LastCheck.ToString("HH:mm:ss")
                        }), cancellationToken);

                    await Task.Delay(3000, cancellationToken); // Check every 3 seconds
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Health monitor error: {ex.Message}");
                }
            }
        }

        private async Task CheckServerHealth(ServerEndpoint server)
        {
            bool wasHealthy = server.IsHealthy;
            server.LastCheck = DateTime.Now;

            // Check if manually partitioned
            if (NetworkPartition.IsPartitioned(server.ServerId))
            {
                server.IsHealthy = false;
                _dataService.UpdateServerStatus(server.ServerId, false, true);
                
                if (wasHealthy)
                {
                    Console.WriteLine($"🔴 {server.ServerId} is PARTITIONED");
                }
                return;
            }

            try
            {
                // Try to connect to the server
                using var channel = GrpcChannel.ForAddress(server.Url);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                
                // Simple health check - channel will throw if server is down
                await Task.Run(() => channel.State, cts.Token);
                
                server.IsHealthy = true;
                _dataService.UpdateServerStatus(server.ServerId, true, false);

                if (!wasHealthy)
                {
                    Console.WriteLine($"🟢 {server.ServerId} is ONLINE");
                    LeaderElection.RestoreNode(server.ServerId);
                }
            }
            catch
            {
                server.IsHealthy = false;
                _dataService.UpdateServerStatus(server.ServerId, false, false);

                if (wasHealthy)
                {
                    Console.WriteLine($"🔴 {server.ServerId} is OFFLINE");
                    LeaderElection.LeaderFailed(server.ServerId);
                }
            }
        }

        public List<ServerEndpoint> GetServerStatus()
        {
            return _servers.ToList();
        }
    }
}