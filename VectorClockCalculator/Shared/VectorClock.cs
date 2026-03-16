using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
    public class VectorClock
    {
        private Dictionary<string, int> _clock;
        private readonly string _nodeId;

        public VectorClock(string nodeId)
        {
            _nodeId = nodeId;
            _clock = new Dictionary<string, int> { { nodeId, 0 } };
        }

        public void Increment()
        {
            _clock[_nodeId]++;
            Console.WriteLine($"[{_nodeId}] Clock incremented: {this}");
        }

        public void Merge(IDictionary<string, int> incomingClock)
        {
            Console.WriteLine($"[{_nodeId}] Merging clock. Before: {this}");
            
            foreach (var kvp in incomingClock)
            {
                if (_clock.ContainsKey(kvp.Key))
                {
                    _clock[kvp.Key] = Math.Max(_clock[kvp.Key], kvp.Value);
                }
                else
                {
                    _clock[kvp.Key] = kvp.Value;
                }
            }
            
            Console.WriteLine($"[{_nodeId}] After merge: {this}");
        }

        public Dictionary<string, int> GetClock()
        {
            return new Dictionary<string, int>(_clock);
        }

        public Dictionary<string, int> CreateSnapshot()
        {
            return new Dictionary<string, int>(_clock);
        }

        public void Restore(Dictionary<string, int> snapshot)
        {
            Console.WriteLine($" [{_nodeId}] ROLLBACK triggered!");
            Console.WriteLine($"[{_nodeId}] Before rollback: {this}");
            _clock = new Dictionary<string, int>(snapshot);
            Console.WriteLine($"[{_nodeId}] After rollback: {this}");
        }

        public bool HappenedBefore(Dictionary<string, int> otherClock)
        {
            bool strictlyLess = false;
            
            foreach (var kvp in _clock)
            {
                if (!otherClock.ContainsKey(kvp.Key))
                {
                    if (kvp.Value > 0) return false;
                }
                else if (kvp.Value > otherClock[kvp.Key])
                {
                    return false;
                }
                else if (kvp.Value < otherClock[kvp.Key])
                {
                    strictlyLess = true;
                }
            }

            foreach (var kvp in otherClock)
            {
                if (!_clock.ContainsKey(kvp.Key) && kvp.Value > 0)
                {
                    strictlyLess = true;
                }
            }

            return strictlyLess;
        }

        public override string ToString()
        {
            var entries = _clock.OrderBy(kvp => kvp.Key)
                                .Select(kvp => $"{kvp.Key}:{kvp.Value}");
            return $"{{{string.Join(", ", entries)}}}";
        }
    }
}