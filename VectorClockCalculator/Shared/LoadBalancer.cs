using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
    public class LoadBalancer
    {
        public enum LoadBalancingStrategy
        {
            RoundRobin,
            Random,
            LeastLoaded,
            LeaderOnly,
            WeightedRandom
        }

        private readonly List<ServerEndpoint> _servers;
        private int _roundRobinIndex = 0;
        private readonly Random _random = new Random();
        private readonly object _lock = new object();
        private LoadBalancingStrategy _strategy = LoadBalancingStrategy.RoundRobin;

        public class ServerEndpoint
        {
            public string ServerId { get; set; } = "";
            public string Url { get; set; } = "";
            public int CurrentLoad { get; set; } = 0;
            public int Weight { get; set; } = 1;
            public bool IsHealthy { get; set; } = true;
        }

        public LoadBalancer()
        {
            _servers = new List<ServerEndpoint>
            {
                new ServerEndpoint { ServerId = "Server1", Url = "http://localhost:5000", Weight = 1 },
                new ServerEndpoint { ServerId = "Server2", Url = "http://localhost:5001", Weight = 1 },
                new ServerEndpoint { ServerId = "Server3", Url = "http://localhost:5002", Weight = 1 }
            };
        }

        public void SetStrategy(LoadBalancingStrategy strategy)
        {
            lock (_lock)
            {
                _strategy = strategy;
                Console.WriteLine($" Load balancing strategy set to: {strategy}");
            }
        }

        public ServerEndpoint? GetNextServer()
        {
            lock (_lock)
            {
                var availableServers = _servers
                    .Where(s => !NetworkPartition.IsPartitioned(s.ServerId) && s.IsHealthy)
                    .ToList();

                if (!availableServers.Any())
                {
                    Console.WriteLine("❌ No available servers for load balancing");
                    return null;
                }

                return _strategy switch
                {
                    LoadBalancingStrategy.RoundRobin => GetRoundRobinServer(availableServers),
                    LoadBalancingStrategy.Random => GetRandomServer(availableServers),
                    LoadBalancingStrategy.LeastLoaded => GetLeastLoadedServer(availableServers),
                    LoadBalancingStrategy.LeaderOnly => GetLeaderServer(availableServers),
                    LoadBalancingStrategy.WeightedRandom => GetWeightedRandomServer(availableServers),
                    _ => GetRoundRobinServer(availableServers)
                };
            }
        }

        private ServerEndpoint GetRoundRobinServer(List<ServerEndpoint> servers)
        {
            var server = servers[_roundRobinIndex % servers.Count];
            _roundRobinIndex = (_roundRobinIndex + 1) % servers.Count;
            Console.WriteLine($" Round-Robin selected: {server.ServerId}");
            return server;
        }

        private ServerEndpoint GetRandomServer(List<ServerEndpoint> servers)
        {
            var server = servers[_random.Next(servers.Count)];
            Console.WriteLine($" Random selected: {server.ServerId}");
            return server;
        }

        private ServerEndpoint GetLeastLoadedServer(List<ServerEndpoint> servers)
        {
            var server = servers.OrderBy(s => s.CurrentLoad).First();
            Console.WriteLine($" Least-loaded selected: {server.ServerId} (load: {server.CurrentLoad})");
            return server;
        }

        private ServerEndpoint GetLeaderServer(List<ServerEndpoint> servers)
        {
            var leader = LeaderElection.GetLeader();
            var leaderServer = servers.FirstOrDefault(s => s.ServerId == leader);
            
            if (leaderServer != null)
            {
                Console.WriteLine($" Main selected: {leaderServer.ServerId}");
                return leaderServer;
            }

            Console.WriteLine(" Leader not available, using fallback");
            return GetRoundRobinServer(servers);
        }

        private ServerEndpoint GetWeightedRandomServer(List<ServerEndpoint> servers)
        {
            var totalWeight = servers.Sum(s => s.Weight);
            var randomValue = _random.Next(totalWeight);
            var cumulativeWeight = 0;

            foreach (var server in servers)
            {
                cumulativeWeight += server.Weight;
                if (randomValue < cumulativeWeight)
                {
                    Console.WriteLine($" Weighted-random selected: {server.ServerId}");
                    return server;
                }
            }

            return servers.Last();
        }

        public void IncrementLoad(string serverId)
        {
            lock (_lock)
            {
                var server = _servers.FirstOrDefault(s => s.ServerId == serverId);
                if (server != null)
                {
                    server.CurrentLoad++;
                }
            }
        }

        public void DecrementLoad(string serverId)
        {
            lock (_lock)
            {
                var server = _servers.FirstOrDefault(s => s.ServerId == serverId);
                if (server != null && server.CurrentLoad > 0)
                {
                    server.CurrentLoad--;
                }
            }
        }

        public void UpdateServerHealth(string serverId, bool isHealthy)
        {
            lock (_lock)
            {
                var server = _servers.FirstOrDefault(s => s.ServerId == serverId);
                if (server != null)
                {
                    server.IsHealthy = isHealthy;
                }
            }
        }

        public List<ServerEndpoint> GetAllServers()
        {
            lock (_lock)
            {
                return new List<ServerEndpoint>(_servers);
            }
        }

        public Dictionary<string, object> GetLoadStatistics()
        {
            lock (_lock)
            {
                return new Dictionary<string, object>
                {
                    { "Strategy", _strategy.ToString() },
                    { "TotalServers", _servers.Count },
                    { "HealthyServers", _servers.Count(s => s.IsHealthy) },
                    { "TotalLoad", _servers.Sum(s => s.CurrentLoad) },
                    { "Servers", _servers.Select(s => new 
                    {
                        s.ServerId,
                        s.CurrentLoad,
                        s.IsHealthy,
                        IsPartitioned = NetworkPartition.IsPartitioned(s.ServerId)
                    }).ToList() }
                };
            }
        }
    }
}