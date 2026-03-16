using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace Shared
{
    public class LeaderElection
    {
        private static readonly string CONFIG_FILE = "leader_config.json";
        private static string? _currentLeader;
        private static readonly object _lock = new object();
        private static readonly List<string> _allNodes = new() { "Server1", "Server2", "Server3" };

        public class LeaderConfig
        {
            public string? Leader { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        public static void Initialize()
        {
            lock (_lock)
            {
                if (File.Exists(CONFIG_FILE))
                {
                    try
                    {
                        var json = File.ReadAllText(CONFIG_FILE);
                        var config = JsonSerializer.Deserialize<LeaderConfig>(json);
                        _currentLeader = config?.Leader;
                        Console.WriteLine($" Main loaded from config: {_currentLeader}");
                    }
                    catch
                    {
                        ElectNewLeader();
                    }
                }
                else
                {
                    ElectNewLeader();
                }
            }
        }

        public static void ElectNewLeader()
        {
            lock (_lock)
            {
                var availableNodes = _allNodes.Where(n => !NetworkPartition.IsPartitioned(n)).ToList();
                
                if (availableNodes.Count == 0)
                {
                    Console.WriteLine(" No available nodes for leader election!");
                    _currentLeader = null;
                    return;
                }

                _currentLeader = availableNodes.OrderByDescending(n => n).First();
                
                var config = new LeaderConfig
                {
                    Leader = _currentLeader,
                    LastUpdated = DateTime.Now
                };

                File.WriteAllText(CONFIG_FILE, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
                
                Console.WriteLine($" NEW LEADER ELECTED: {_currentLeader}");
                Console.WriteLine($" Available nodes: {string.Join(", ", availableNodes)}");
            }
        }

        public static string? GetLeader()
        {
            lock (_lock)
            {
                return _currentLeader;
            }
        }

        public static bool IsLeader(string nodeId)
        {
            lock (_lock)
            {
                return _currentLeader == nodeId;
            }
        }

        public static void LeaderFailed(string nodeId)
        {
            lock (_lock)
            {
                if (_currentLeader == nodeId)
                {
                    Console.WriteLine($"❌ Leader {nodeId} has failed!");
                    NetworkPartition.PartitionNode(nodeId);
                    ElectNewLeader();
                }
            }
        }

        public static void RestoreNode(string nodeId)
        {
            lock (_lock)
            {
                NetworkPartition.ReconnectNode(nodeId);
                
                if (string.Compare(nodeId, _currentLeader) > 0)
                {
                    Console.WriteLine($" Restored node {nodeId} has higher priority. Re-electing...");
                    ElectNewLeader();
                }
            }
        }

        public static List<string> GetAllNodes()
        {
            return new List<string>(_allNodes);
        }
    }
}