using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shared
{
    public class ErrorSimulator
    {
        private readonly Random _random = new Random();
        private readonly string _nodeId;

        public double RandomFailureRate { get; set; } = 0.25;
        public double NetworkDelayProbability { get; set; } = 0.15;
        public double TimeoutProbability { get; set; } = 0.10;
        public int MinNetworkDelay { get; set; } = 1000;
        public int MaxNetworkDelay { get; set; } = 3000;
        public int TimeoutDuration { get; set; } = 10000;

        public ErrorSimulator(string nodeId)
        {
            _nodeId = nodeId;
        }

        public bool ShouldFail(double? inputValue = null)
        {
            if (inputValue.HasValue && inputValue.Value < 0)
            {
                Console.WriteLine($" [{_nodeId}] ERROR: Negative number ({inputValue})");
                return true;
            }

            if (_random.NextDouble() < RandomFailureRate)
            {
                Console.WriteLine($" [{_nodeId}] ERROR: Random failure triggered ({RandomFailureRate * 100}%)");
                return true;
            }

            return false;
        }

        public async Task SimulateNetworkConditions()
        {
            if (_random.NextDouble() < NetworkDelayProbability)
            {
                int delay = _random.Next(MinNetworkDelay, MaxNetworkDelay);
                Console.WriteLine($"🌐 [{_nodeId}] Network delay: {delay}ms");
                await Task.Delay(delay);
            }

            if (_random.NextDouble() < TimeoutProbability)
            {
                Console.WriteLine($"⏱️ [{_nodeId}] Timeout: {TimeoutDuration}ms");
                await Task.Delay(TimeoutDuration);
                throw new TimeoutException($"Operation timed out after {TimeoutDuration}ms");
            }
        }

        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                { "NodeId", _nodeId },
                { "RandomFailureRate", $"{RandomFailureRate * 100}%" },
                { "NetworkDelayProbability", $"{NetworkDelayProbability * 100}%" },
                { "TimeoutProbability", $"{TimeoutProbability * 100}%" }
            };
        }
    }
}