using System;
using System.Collections.Generic;

namespace Shared
{
    public class NetworkPartition
    {
        private static readonly Dictionary<string, bool> _partitionedNodes = new();
        private static readonly object _lock = new object();

        public static void PartitionNode(string nodeId)
        {
            lock (_lock)
            {
                _partitionedNodes[nodeId] = true;
                Console.WriteLine($"🔴 PARTITION: {nodeId} is now DISCONNECTED from the network");
            }
        }

        public static void ReconnectNode(string nodeId)
        {
            lock (_lock)
            {
                _partitionedNodes[nodeId] = false;
                Console.WriteLine($"🟢 RECONNECT: {nodeId} is now CONNECTED to the network");
            }
        }

        public static bool IsPartitioned(string nodeId)
        {
            lock (_lock)
            {
                return _partitionedNodes.ContainsKey(nodeId) && _partitionedNodes[nodeId];
            }
        }

        public static void CheckPartition(string nodeId)
        {
            if (IsPartitioned(nodeId))
            {
                Console.WriteLine($" [{nodeId}] Network partition detected! Node is isolated.");
                throw new Exception($"Network partition: {nodeId} is unreachable");
            }
        }

        public static Dictionary<string, bool> GetPartitionStatus()
        {
            lock (_lock)
            {
                return new Dictionary<string, bool>(_partitionedNodes);
            }
        }
    }
}