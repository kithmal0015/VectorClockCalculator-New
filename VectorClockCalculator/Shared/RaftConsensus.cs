using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Shared
{
    /// <summary>
    /// Simplified Raft Consensus Algorithm for Leader Election
    /// States: Follower, Candidate, Leader
    /// </summary>
    public class RaftConsensus
    {
        public enum NodeState
        {
            Follower,
            Candidate,
            Leader
        }

        public class RaftNode
        {
            public string NodeId { get; set; } = "";
            public NodeState State { get; set; } = NodeState.Follower;
            public int CurrentTerm { get; set; } = 0;
            public string? VotedFor { get; set; } = null;
            public int VotesReceived { get; set; } = 0;
            public DateTime LastHeartbeat { get; set; } = DateTime.Now;
        }

        private readonly string _nodeId;
        private RaftNode _node;
        private readonly List<string> _allNodes;
        private readonly Random _random;
        private CancellationTokenSource? _cts;
        private readonly int _electionTimeoutMin = 3000; // 3 seconds
        private readonly int _electionTimeoutMax = 6000; // 6 seconds
        private readonly int _heartbeatInterval = 1000; // 1 second
        private readonly object _lock = new object();

        public RaftConsensus(string nodeId, List<string> allNodes)
        {
            _nodeId = nodeId;
            _allNodes = new List<string>(allNodes);
            _random = new Random();
            _node = new RaftNode { NodeId = nodeId };
        }

        /// <summary>
        /// Start the Raft consensus algorithm
        /// </summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => RunRaftLoop(_cts.Token));
            Console.WriteLine($" [{_nodeId}] Raft consensus started");
        }

        /// <summary>
        /// Stop the Raft algorithm
        /// </summary>
        public void Stop()
        {
            _cts?.Cancel();
            Console.WriteLine($"🛑 [{_nodeId}] Raft consensus stopped");
        }

        private async Task RunRaftLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    lock (_lock)
                    {
                        switch (_node.State)
                        {
                            case NodeState.Follower:
                                HandleFollowerState();
                                break;
                            case NodeState.Candidate:
                                HandleCandidateState();
                                break;
                            case NodeState.Leader:
                                HandleLeaderState();
                                break;
                        }
                    }

                    await Task.Delay(100, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private void HandleFollowerState()
        {
            // Check if election timeout has passed
            var timeSinceHeartbeat = (DateTime.Now - _node.LastHeartbeat).TotalMilliseconds;
            var electionTimeout = _random.Next(_electionTimeoutMin, _electionTimeoutMax);

            if (timeSinceHeartbeat > electionTimeout)
            {
                // No heartbeat received, become candidate
                BecomeCandidate();
            }
        }

        private void HandleCandidateState()
        {
            // Start election
            StartElection();
        }

        private void HandleLeaderState()
        {
            // Send heartbeats to all followers
            SendHeartbeats();
        }

        private void BecomeCandidate()
        {
            _node.State = NodeState.Candidate;
            _node.CurrentTerm++;
            _node.VotedFor = _nodeId;
            _node.VotesReceived = 1; // Vote for self
            _node.LastHeartbeat = DateTime.Now;

            Console.WriteLine($" [{_nodeId}] Became CANDIDATE for term {_node.CurrentTerm}");
        }

        private void StartElection()
        {
            Console.WriteLine($" [{_nodeId}] Starting election for term {_node.CurrentTerm}");

            // Request votes from other nodes
            var availableNodes = _allNodes.Where(n => n != _nodeId && !NetworkPartition.IsPartitioned(n)).ToList();
            
            foreach (var node in availableNodes)
            {
                // Simulate vote request (in real system, this would be RPC)
                bool voteGranted = SimulateVoteRequest(node);
                
                if (voteGranted)
                {
                    _node.VotesReceived++;
                    Console.WriteLine($"✅ [{_nodeId}] Received vote from {node} ({_node.VotesReceived}/{_allNodes.Count})");
                }
            }

            // Check if we have majority
            int majority = (_allNodes.Count / 2) + 1;
            if (_node.VotesReceived >= majority)
            {
                BecomeLeader();
            }
            else
            {
                // Lost election, go back to follower
                Console.WriteLine($"❌ [{_nodeId}] Lost election with {_node.VotesReceived} votes (needed {majority})");
                BecomeFollower();
            }
        }

        private bool SimulateVoteRequest(string targetNode)
        {
            // Simulate vote decision (in real system, follower would decide)
            // Vote is granted if the node hasn't voted in this term
            return _random.Next(100) > 30; // 70% chance of granting vote
        }

        private void BecomeLeader()
        {
            _node.State = NodeState.Leader;
            Console.WriteLine($" [{_nodeId}] Became LEADER for term {_node.CurrentTerm}");
            
            // Update leader election system
            LeaderElection.Initialize();
        }

        private void BecomeFollower()
        {
            _node.State = NodeState.Follower;
            _node.VotedFor = null;
            _node.VotesReceived = 0;
            _node.LastHeartbeat = DateTime.Now;
            Console.WriteLine($" [{_nodeId}] Became FOLLOWER");
        }

        private void SendHeartbeats()
        {
            var availableNodes = _allNodes.Where(n => n != _nodeId && !NetworkPartition.IsPartitioned(n)).ToList();
            
            foreach (var node in availableNodes)
            {
                // Simulate heartbeat (in real system, this would be RPC)
                Console.WriteLine($" [{_nodeId}] Sent heartbeat to {node} (term {_node.CurrentTerm})");
            }
        }

        /// <summary>
        /// Receive heartbeat from leader
        /// </summary>
        public void ReceiveHeartbeat(string leaderId, int term)
        {
            lock (_lock)
            {
                if (term >= _node.CurrentTerm)
                {
                    _node.CurrentTerm = term;
                    _node.LastHeartbeat = DateTime.Now;
                    
                    if (_node.State != NodeState.Follower)
                    {
                        BecomeFollower();
                    }

                    Console.WriteLine($" [{_nodeId}] Received heartbeat from {leaderId} (term {term})");
                }
            }
        }

        /// <summary>
        /// Get current state
        /// </summary>
        public (NodeState State, int Term, bool IsLeader) GetState()
        {
            lock (_lock)
            {
                return (_node.State, _node.CurrentTerm, _node.State == NodeState.Leader);
            }
        }

        /// <summary>
        /// Get detailed status
        /// </summary>
        public string GetStatus()
        {
            lock (_lock)
            {
                return $"[{_nodeId}] State: {_node.State}, Term: {_node.CurrentTerm}, " +
                       $"Votes: {_node.VotesReceived}, Last Heartbeat: {(DateTime.Now - _node.LastHeartbeat).TotalSeconds:F1}s ago";
            }
        }
    }
}