using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Shared
{
    public class GossipProtocol
    {
        private readonly string _nodeId;
        private readonly VectorClock _clock;
        private readonly Dictionary<string, Dictionary<string, int>> _peerClocks;
        private CancellationTokenSource? _cts;
        private readonly Random _random;
        private const int GOSSIP_INTERVAL_MS = 10000;
        private DateTime _startTime;

        public GossipProtocol(string nodeId, VectorClock clock)
        {
            _nodeId = nodeId;
            _clock = clock;
            _peerClocks = new Dictionary<string, Dictionary<string, int>>();
            _random = new Random();
            _startTime = DateTime.Now;
        }

        public void StartGossip()
        {
            _cts = new CancellationTokenSource();
            _startTime = DateTime.Now;
            Task.Run(() => GossipLoop(_cts.Token));
            Console.WriteLine($" [{_nodeId}] Gossip protocol started (interval: {GOSSIP_INTERVAL_MS}ms)");
        }

        public void StopGossip()
        {
            _cts?.Cancel();
            Console.WriteLine($"🛑 [{_nodeId}] Gossip protocol stopped");
        }

        private async Task GossipLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(GOSSIP_INTERVAL_MS, token);
                    PerformGossip();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private void PerformGossip()
        {
            var availablePeers = LeaderElection.GetAllNodes()
                .Where(n => n != _nodeId && !NetworkPartition.IsPartitioned(n))
                .ToList();

            if (availablePeers.Count == 0)
            {
                Console.WriteLine($" [{_nodeId}] No peers available for gossip");
                return;
            }

            var randomPeer = availablePeers[_random.Next(availablePeers.Count)];

            Console.WriteLine($"\n [{_nodeId}] Gossiping with {randomPeer}...");
            Console.WriteLine($" Sending clock: {_clock}");

            SendClockToPeer(randomPeer);
            CheckConvergence();
        }

        private void SendClockToPeer(string peerId)
        {
            var myClock = _clock.GetClock();
            
            if (!_peerClocks.ContainsKey(peerId))
            {
                _peerClocks[peerId] = new Dictionary<string, int>(myClock);
            }
            else
            {
                foreach (var kvp in myClock)
                {
                    if (!_peerClocks[peerId].ContainsKey(kvp.Key))
                    {
                        _peerClocks[peerId][kvp.Key] = kvp.Value;
                    }
                    else
                    {
                        _peerClocks[peerId][kvp.Key] = Math.Max(_peerClocks[peerId][kvp.Key], kvp.Value);
                    }
                }
            }

            Console.WriteLine($" {peerId} received clock update");
        }

        public void ReceiveGossip(string peerId, Dictionary<string, int> peerClock)
        {
            Console.WriteLine($" [{_nodeId}] Received gossip from {peerId}");
            Console.WriteLine($" Received clock: {string.Join(", ", peerClock.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");
            
            _peerClocks[peerId] = new Dictionary<string, int>(peerClock);
            _clock.Merge(peerClock);
        }

        private void CheckConvergence()
        {
            var myClock = _clock.GetClock();
            bool allConverged = true;
            int maxDiff = 0;

            foreach (var peer in _peerClocks)
            {
                foreach (var kvp in myClock)
                {
                    if (peer.Value.ContainsKey(kvp.Key))
                    {
                        int diff = Math.Abs(kvp.Value - peer.Value[kvp.Key]);
                        maxDiff = Math.Max(maxDiff, diff);
                        
                        if (diff > 2)
                        {
                            allConverged = false;
                        }
                    }
                }
            }

            var elapsedTime = (DateTime.Now - _startTime).TotalSeconds;
            
            if (allConverged)
            {
                Console.WriteLine($"✅ [{_nodeId}] All nodes converged! (Time: {elapsedTime:F1}s, Max diff: {maxDiff})");
            }
            else
            {
                Console.WriteLine($" [{_nodeId}] Convergence in progress... (Time: {elapsedTime:F1}s, Max diff: {maxDiff})");
            }
        }
    }
}