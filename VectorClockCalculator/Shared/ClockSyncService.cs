using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Shared
{
    public class ClockSyncService
    {
        private readonly string _nodeId;
        private readonly VectorClock _clock;
        private Dictionary<string, VectorClock> _peerClocks;
        private CancellationTokenSource? _cts;
        private const int SYNC_INTERVAL_MS = 10000;
        private const int DIVERGENCE_THRESHOLD_SECONDS = 5;

        public ClockSyncService(string nodeId, VectorClock clock)
        {
            _nodeId = nodeId;
            _clock = clock;
            _peerClocks = new Dictionary<string, VectorClock>();
        }

        public void StartPeriodicSync()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => PeriodicSyncLoop(_cts.Token));
            Console.WriteLine($" [{_nodeId}] Clock sync service started (interval: {SYNC_INTERVAL_MS}ms)");
        }

        public void StopPeriodicSync()
        {
            _cts?.Cancel();
            Console.WriteLine($"🛑 [{_nodeId}] Clock sync service stopped");
        }

        private async Task PeriodicSyncLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(SYNC_INTERVAL_MS, token);
                    PerformSync();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public void PerformSync()
        {
            Console.WriteLine($"\n [{_nodeId}] Performing clock sync...");
            
            bool isDiverged = CheckDivergence();
            
            if (isDiverged)
            {
                Console.WriteLine($" [{_nodeId}] DIVERGED - Clocks are out of sync!");
            }
            else
            {
                Console.WriteLine($"✅ [{_nodeId}] Clocks are synchronized");
            }

            Console.WriteLine($" [{_nodeId}] Current clock: {_clock}");
        }

        public void RegisterPeerClock(string peerId, Dictionary<string, int> peerClock)
        {
            var clock = new VectorClock(peerId);
            foreach (var kvp in peerClock)
            {
                clock.Merge(new Dictionary<string, int> { { kvp.Key, kvp.Value } });
            }
            _peerClocks[peerId] = clock;
        }

        private bool CheckDivergence()
        {
            var myClock = _clock.GetClock();
            
            foreach (var peer in _peerClocks)
            {
                var peerClock = peer.Value.GetClock();
                
                foreach (var kvp in myClock)
                {
                    if (peerClock.ContainsKey(kvp.Key))
                    {
                        int diff = Math.Abs(kvp.Value - peerClock[kvp.Key]);
                        if (diff > DIVERGENCE_THRESHOLD_SECONDS)
                        {
                            Console.WriteLine($" [{_nodeId}] Divergence detected with {peer.Key}: " +
                                            $"{kvp.Key} diff = {diff} (threshold: {DIVERGENCE_THRESHOLD_SECONDS})");
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }

        public Dictionary<string, int> GetLatestClock()
        {
            var latestClock = new Dictionary<string, int>(_clock.GetClock());
            
            foreach (var peer in _peerClocks)
            {
                var peerClock = peer.Value.GetClock();
                foreach (var kvp in peerClock)
                {
                    if (!latestClock.ContainsKey(kvp.Key) || latestClock[kvp.Key] < kvp.Value)
                    {
                        latestClock[kvp.Key] = kvp.Value;
                    }
                }
            }
            
            return latestClock;
        }
    }
}