using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
    /// <summary>
    /// Automatic conflict resolution for distributed operations
    /// Implements multiple strategies: Last-Write-Wins, Highest-Value, Merge, Custom
    /// </summary>
    public class ConflictResolver
    {
        public enum ResolutionStrategy
        {
            LastWriteWins,      // Use the most recent timestamp
            HighestValue,       // Use the highest value
            LowestValue,        // Use the lowest value
            Average,            // Average all conflicting values
            Merge,              // Merge all values (for lists/sets)
            Custom              // Custom resolution logic
        }

        public class ConflictingOperation
        {
            public string NodeId { get; set; } = "";
            public double Value { get; set; }
            public DateTime Timestamp { get; set; }
            public Dictionary<string, int> VectorClock { get; set; } = new();
            public string OperationType { get; set; } = "";
        }

        public class ResolutionResult
        {
            public double ResolvedValue { get; set; }
            public string Strategy { get; set; } = "";
            public string WinningNode { get; set; } = "";
            public string Reason { get; set; } = "";
            public List<ConflictingOperation> DiscardedOperations { get; set; } = new();
        }

        private readonly ResolutionStrategy _strategy;
        private readonly Func<List<ConflictingOperation>, ResolutionResult>? _customResolver;

        public ConflictResolver(ResolutionStrategy strategy, 
            Func<List<ConflictingOperation>, ResolutionResult>? customResolver = null)
        {
            _strategy = strategy;
            _customResolver = customResolver;
        }

        /// <summary>
        /// Resolve conflicts between multiple operations
        /// </summary>
        public ResolutionResult ResolveConflict(List<ConflictingOperation> operations)
        {
            if (operations == null || operations.Count == 0)
            {
                throw new ArgumentException("No operations to resolve");
            }

            if (operations.Count == 1)
            {
                // No conflict
                return new ResolutionResult
                {
                    ResolvedValue = operations[0].Value,
                    Strategy = "No Conflict",
                    WinningNode = operations[0].NodeId,
                    Reason = "Only one operation present"
                };
            }

            Console.WriteLine($"\n CONFLICT DETECTED: {operations.Count} conflicting operations");
            foreach (var op in operations)
            {
                Console.WriteLine($"   - {op.NodeId}: Value={op.Value}, Time={op.Timestamp:HH:mm:ss.fff}");
            }

            ResolutionResult result = _strategy switch
            {
                ResolutionStrategy.LastWriteWins => ResolveLastWriteWins(operations),
                ResolutionStrategy.HighestValue => ResolveHighestValue(operations),
                ResolutionStrategy.LowestValue => ResolveLowestValue(operations),
                ResolutionStrategy.Average => ResolveAverage(operations),
                ResolutionStrategy.Merge => ResolveMerge(operations),
                ResolutionStrategy.Custom => ResolveCustom(operations),
                _ => ResolveLastWriteWins(operations)
            };

            Console.WriteLine($"✅ CONFLICT RESOLVED: Strategy={result.Strategy}, Winner={result.WinningNode}, Value={result.ResolvedValue}");
            Console.WriteLine($"   Reason: {result.Reason}");

            return result;
        }

        private ResolutionResult ResolveLastWriteWins(List<ConflictingOperation> operations)
        {
            var winner = operations.OrderByDescending(o => o.Timestamp).First();
            var discarded = operations.Where(o => o != winner).ToList();

            return new ResolutionResult
            {
                ResolvedValue = winner.Value,
                Strategy = "Last-Write-Wins",
                WinningNode = winner.NodeId,
                Reason = $"Most recent timestamp: {winner.Timestamp:HH:mm:ss.fff}",
                DiscardedOperations = discarded
            };
        }

        private ResolutionResult ResolveHighestValue(List<ConflictingOperation> operations)
        {
            var winner = operations.OrderByDescending(o => o.Value).First();
            var discarded = operations.Where(o => o != winner).ToList();

            return new ResolutionResult
            {
                ResolvedValue = winner.Value,
                Strategy = "Highest-Value",
                WinningNode = winner.NodeId,
                Reason = $"Highest value: {winner.Value}",
                DiscardedOperations = discarded
            };
        }

        private ResolutionResult ResolveLowestValue(List<ConflictingOperation> operations)
        {
            var winner = operations.OrderBy(o => o.Value).First();
            var discarded = operations.Where(o => o != winner).ToList();

            return new ResolutionResult
            {
                ResolvedValue = winner.Value,
                Strategy = "Lowest-Value",
                WinningNode = winner.NodeId,
                Reason = $"Lowest value: {winner.Value}",
                DiscardedOperations = discarded
            };
        }

        private ResolutionResult ResolveAverage(List<ConflictingOperation> operations)
        {
            var average = operations.Average(o => o.Value);
            var closest = operations.OrderBy(o => Math.Abs(o.Value - average)).First();

            return new ResolutionResult
            {
                ResolvedValue = average,
                Strategy = "Average",
                WinningNode = "System",
                Reason = $"Average of all values: {average:F2} (closest: {closest.NodeId})",
                DiscardedOperations = new List<ConflictingOperation>()
            };
        }

        private ResolutionResult ResolveMerge(List<ConflictingOperation> operations)
        {
            // For numeric operations, we can merge by summing
            var sum = operations.Sum(o => o.Value);

            return new ResolutionResult
            {
                ResolvedValue = sum,
                Strategy = "Merge",
                WinningNode = "System",
                Reason = $"Merged all operations: sum = {sum}",
                DiscardedOperations = new List<ConflictingOperation>()
            };
        }

        private ResolutionResult ResolveCustom(List<ConflictingOperation> operations)
        {
            if (_customResolver == null)
            {
                throw new InvalidOperationException("Custom resolver not provided");
            }

            return _customResolver(operations);
        }

        /// <summary>
        /// Detect conflicts using vector clocks
        /// </summary>
        public static bool IsConflict(Dictionary<string, int> clock1, Dictionary<string, int> clock2)
        {
            bool clock1Less = false;
            bool clock2Less = false;

            var allKeys = clock1.Keys.Union(clock2.Keys);

            foreach (var key in allKeys)
            {
                int val1 = clock1.ContainsKey(key) ? clock1[key] : 0;
                int val2 = clock2.ContainsKey(key) ? clock2[key] : 0;

                if (val1 < val2) clock2Less = true;
                if (val1 > val2) clock1Less = true;
            }

            // Conflict if neither happened before the other (concurrent)
            return clock1Less && clock2Less;
        }

        /// <summary>
        /// Create a conflict resolver with priority-based custom logic
        /// </summary>
        public static ConflictResolver CreatePriorityResolver(Dictionary<string, int> nodePriorities)
        {
            return new ConflictResolver(ResolutionStrategy.Custom, operations =>
            {
                var winner = operations
                    .OrderByDescending(o => nodePriorities.ContainsKey(o.NodeId) ? nodePriorities[o.NodeId] : 0)
                    .ThenByDescending(o => o.Timestamp)
                    .First();

                var discarded = operations.Where(o => o != winner).ToList();

                return new ResolutionResult
                {
                    ResolvedValue = winner.Value,
                    Strategy = "Priority-Based",
                    WinningNode = winner.NodeId,
                    Reason = $"Highest priority node: {winner.NodeId}",
                    DiscardedOperations = discarded
                };
            });
        }

        /// <summary>
        /// Create a conflict resolver based on vector clock causality
        /// </summary>
        public static ConflictResolver CreateCausalityResolver()
        {
            return new ConflictResolver(ResolutionStrategy.Custom, operations =>
            {
                // Find operation that happened most recently in causal order
                var winner = operations[0];

                foreach (var op in operations.Skip(1))
                {
                    if (HappenedBefore(winner.VectorClock, op.VectorClock))
                    {
                        winner = op; // This operation is more recent causally
                    }
                }

                var discarded = operations.Where(o => o != winner).ToList();

                return new ResolutionResult
                {
                    ResolvedValue = winner.Value,
                    Strategy = "Causality-Based",
                    WinningNode = winner.NodeId,
                    Reason = "Most recent in causal order",
                    DiscardedOperations = discarded
                };
            });
        }

        private static bool HappenedBefore(Dictionary<string, int> clock1, Dictionary<string, int> clock2)
        {
            bool strictlyLess = false;
            var allKeys = clock1.Keys.Union(clock2.Keys);

            foreach (var key in allKeys)
            {
                int val1 = clock1.ContainsKey(key) ? clock1[key] : 0;
                int val2 = clock2.ContainsKey(key) ? clock2[key] : 0;

                if (val1 > val2) return false;
                if (val1 < val2) strictlyLess = true;
            }

            return strictlyLess;
        }
    }
}