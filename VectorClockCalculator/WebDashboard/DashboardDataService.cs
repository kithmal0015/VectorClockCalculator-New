using Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WebDashboard
{
    public class OperationHistory
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; }
        public string ClientId { get; set; } = "";
        public string ServerId { get; set; } = "";
        public string Operation { get; set; } = "";
        public double Input1 { get; set; }
        public double Input2 { get; set; }
        public double Result { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public Dictionary<string, int> VectorClock { get; set; } = new();
        public int RetryCount { get; set; } = 0;
    }

    public class ServerInfo
    {
        public string ServerId { get; set; } = "";
        public bool IsOnline { get; set; }
        public bool IsLeader { get; set; }
        public bool IsPartitioned { get; set; }
        public Dictionary<string, int> VectorClock { get; set; } = new();
        public DateTime LastHeartbeat { get; set; }
        public int TotalOperations { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public double SuccessRate { get; set; }
    }

    public class DashboardDataService
    {
        private readonly Dictionary<string, ServerInfo> _servers = new();
        private readonly List<OperationHistory> _operationHistory = new();
        private readonly object _lock = new object();
        private const int MAX_HISTORY = 100;

        public DashboardDataService()
        {
            // Initialize servers
            InitializeServers();
        }

        private void InitializeServers()
        {
            var serverIds = new[] { "Server1", "Server2", "Server3" };
            foreach (var id in serverIds)
            {
                _servers[id] = new ServerInfo
                {
                    ServerId = id,
                    IsOnline = true,
                    IsLeader = false,
                    IsPartitioned = false,
                    LastHeartbeat = DateTime.Now,
                    VectorClock = new Dictionary<string, int> { { id, 0 } }
                };
            }
        }

        public void UpdateServerStatus(string serverId, bool isOnline, bool isPartitioned)
        {
            lock (_lock)
            {
                if (_servers.ContainsKey(serverId))
                {
                    _servers[serverId].IsOnline = isOnline;
                    _servers[serverId].IsPartitioned = isPartitioned;
                    _servers[serverId].LastHeartbeat = DateTime.Now;
                }
            }
        }

        public void UpdateVectorClock(string serverId, Dictionary<string, int> clock)
        {
            lock (_lock)
            {
                if (_servers.ContainsKey(serverId))
                {
                    _servers[serverId].VectorClock = new Dictionary<string, int>(clock);
                    _servers[serverId].LastHeartbeat = DateTime.Now;
                }
            }
        }

        public void AddOperation(OperationHistory operation)
        {
            lock (_lock)
            {
                _operationHistory.Insert(0, operation);
                
                // Update server stats
                if (_servers.ContainsKey(operation.ServerId))
                {
                    var server = _servers[operation.ServerId];
                    server.TotalOperations++;
                    
                    if (operation.Success)
                        server.SuccessfulOperations++;
                    else
                        server.FailedOperations++;
                    
                    server.SuccessRate = server.TotalOperations > 0 
                        ? (double)server.SuccessfulOperations / server.TotalOperations * 100 
                        : 0;
                }

                // Keep only last MAX_HISTORY operations
                if (_operationHistory.Count > MAX_HISTORY)
                {
                    _operationHistory.RemoveAt(_operationHistory.Count - 1);
                }
            }
        }

        public object GetSystemStatus()
        {
            lock (_lock)
            {
                var leader = LeaderElection.GetLeader();
                var partitions = NetworkPartition.GetPartitionStatus();

                // Update leader status
                foreach (var server in _servers.Values)
                {
                    server.IsLeader = server.ServerId == leader;
                    server.IsPartitioned = partitions.ContainsKey(server.ServerId) && partitions[server.ServerId];
                }

                var totalOps = _operationHistory.Count;
                var successOps = _operationHistory.Count(o => o.Success);
                var failedOps = _operationHistory.Count(o => !o.Success);

                return new
                {
                    Timestamp = DateTime.Now,
                    Leader = leader,
                    Servers = _servers.Values.ToList(),
                    RecentOperations = _operationHistory.Take(20).ToList(),
                    Statistics = new
                    {
                        TotalOperations = totalOps,
                        SuccessfulOperations = successOps,
                        FailedOperations = failedOps,
                        SuccessRate = totalOps > 0 ? (double)successOps / totalOps * 100 : 0,
                        ActiveServers = _servers.Values.Count(s => s.IsOnline && !s.IsPartitioned),
                        PartitionedServers = _servers.Values.Count(s => s.IsPartitioned)
                    }
                };
            }
        }

        public List<OperationHistory> GetOperationHistory(int count = 50)
        {
            lock (_lock)
            {
                return _operationHistory.Take(count).ToList();
            }
        }

        public ServerInfo? GetServerInfo(string serverId)
        {
            lock (_lock)
            {
                return _servers.ContainsKey(serverId) ? _servers[serverId] : null;
            }
        }

        public void ClearHistory()
        {
            lock (_lock)
            {
                _operationHistory.Clear();
                foreach (var server in _servers.Values)
                {
                    server.TotalOperations = 0;
                    server.SuccessfulOperations = 0;
                    server.FailedOperations = 0;
                    server.SuccessRate = 0;
                }
            }
        }
    }
}