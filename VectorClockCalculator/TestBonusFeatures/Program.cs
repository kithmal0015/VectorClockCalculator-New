using Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestBonusFeatures
{
    class Program
    {
        static async Task Main(string[] args)
        {
            
            Console.WriteLine("BONUS FEATURES TEST SUITE");
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine("\n" + new string('═', 60));
                Console.WriteLine("SELECT TEST:");
                Console.WriteLine("  1. Test Matrix Clocks");
                Console.WriteLine("  2. Test Raft Consensus");
                Console.WriteLine("  3. Test Conflict Resolution");
                Console.WriteLine("  4. Run All Tests");
                Console.WriteLine("  q. Quit");
                Console.WriteLine(new string('═', 60));
                Console.Write("\nEnter choice: ");

                string? choice = Console.ReadLine();
                if (choice?.ToLower() == "q") break;

                switch (choice)
                {
                    case "1":
                        TestMatrixClocks();
                        break;
                    case "2":
                        await TestRaftConsensus();
                        break;
                    case "3":
                        TestConflictResolution();
                        break;
                    case "4":
                        TestMatrixClocks();
                        await Task.Delay(2000);
                        await TestRaftConsensus();
                        await Task.Delay(2000);
                        TestConflictResolution();
                        break;
                    default:
                        Console.WriteLine("❌ Invalid choice!");
                        break;
                }
            }

            Console.WriteLine("\n Tests completed!");
        }

        static void TestMatrixClocks()
        {
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine("TESTING MATRIX CLOCKS");
            Console.WriteLine(new string('═', 60));

            var nodes = new List<string> { "Server1", "Server2", "Server3" };
            
            var matrix1 = new MatrixClock("Server1", nodes);
            var matrix2 = new MatrixClock("Server2", nodes);
            var matrix3 = new MatrixClock("Server3", nodes);

            Console.WriteLine("\n Initial State:");
            Console.WriteLine(matrix1.ToMatrixString());

            // Simulate operations
            Console.WriteLine("\n Server1 performs operation:");
            matrix1.Increment();

            Console.WriteLine("\n Server2 performs operation:");
            matrix2.Increment();

            Console.WriteLine("\n Server1 and Server2 communicate:");
            matrix1.Merge(matrix2.GetMatrix());
            matrix2.Merge(matrix1.GetMatrix());

            Console.WriteLine(matrix1.ToMatrixString());
            Console.WriteLine(matrix2.ToMatrixString());

            Console.WriteLine("\n Server3 performs operation:");
            matrix3.Increment();

            Console.WriteLine("\n All servers synchronize:");
            matrix3.Merge(matrix1.GetMatrix());
            matrix1.Merge(matrix3.GetMatrix());
            matrix2.Merge(matrix3.GetMatrix());

            Console.WriteLine(matrix3.ToMatrixString());

            // Check global causality
            Console.WriteLine("\n Checking global causality:");
            Console.WriteLine($"Server1 can determine global causality: {matrix1.CanDetermineGlobalCausality()}");
            Console.WriteLine($"Server2 can determine global causality: {matrix2.CanDetermineGlobalCausality()}");
            Console.WriteLine($"Server3 can determine global causality: {matrix3.CanDetermineGlobalCausality()}");

            Console.WriteLine("\n✅ Matrix Clock Test Complete!");
        }

        static async Task TestRaftConsensus()
        {
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine(" TESTING RAFT CONSENSUS");
            Console.WriteLine(new string('═', 60));

            var nodes = new List<string> { "Server1", "Server2", "Server3" };
            
            var raft1 = new RaftConsensus("Server1", nodes);
            var raft2 = new RaftConsensus("Server2", nodes);
            var raft3 = new RaftConsensus("Server3", nodes);

            Console.WriteLine("\n Starting Raft nodes...");
            raft1.Start();
            raft2.Start();
            raft3.Start();

            Console.WriteLine("\n Waiting for leader election (10 seconds)...");
            await Task.Delay(10000);

            Console.WriteLine("\n Final Status:");
            Console.WriteLine(raft1.GetStatus());
            Console.WriteLine(raft2.GetStatus());
            Console.WriteLine(raft3.GetStatus());

            var (state1, term1, isLeader1) = raft1.GetState();
            var (state2, term2, isLeader2) = raft2.GetState();
            var (state3, term3, isLeader3) = raft3.GetState();

            Console.WriteLine($"\n Leader: {(isLeader1 ? "Server1" : isLeader2 ? "Server2" : isLeader3 ? "Server3" : "None")}");

            Console.WriteLine("\n🛑 Stopping Raft nodes...");
            raft1.Stop();
            raft2.Stop();
            raft3.Stop();

            Console.WriteLine("\n✅ Raft Consensus Test Complete!");
        }

        static void TestConflictResolution()
        {
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine(" TESTING CONFLICT RESOLUTION");
            Console.WriteLine(new string('═', 60));

            // Create conflicting operations
            var operations = new List<ConflictResolver.ConflictingOperation>
            {
                new ConflictResolver.ConflictingOperation
                {
                    NodeId = "Server1",
                    Value = 100,
                    Timestamp = DateTime.Now.AddSeconds(-3),
                    VectorClock = new Dictionary<string, int> { {"Server1", 5}, {"Server2", 2}, {"Server3", 1} }
                },
                new ConflictResolver.ConflictingOperation
                {
                    NodeId = "Server2",
                    Value = 150,
                    Timestamp = DateTime.Now.AddSeconds(-2),
                    VectorClock = new Dictionary<string, int> { {"Server1", 3}, {"Server2", 6}, {"Server3", 2} }
                },
                new ConflictResolver.ConflictingOperation
                {
                    NodeId = "Server3",
                    Value = 120,
                    Timestamp = DateTime.Now.AddSeconds(-1),
                    VectorClock = new Dictionary<string, int> { {"Server1", 4}, {"Server2", 5}, {"Server3", 8} }
                }
            };

            // Test different strategies
            Console.WriteLine("\n Strategy 1: Last-Write-Wins");
            var resolver1 = new ConflictResolver(ConflictResolver.ResolutionStrategy.LastWriteWins);
            var result1 = resolver1.ResolveConflict(operations);

            Console.WriteLine("\n Strategy 2: Highest-Value");
            var resolver2 = new ConflictResolver(ConflictResolver.ResolutionStrategy.HighestValue);
            var result2 = resolver2.ResolveConflict(operations);

            Console.WriteLine("\n Strategy 3: Average");
            var resolver3 = new ConflictResolver(ConflictResolver.ResolutionStrategy.Average);
            var result3 = resolver3.ResolveConflict(operations);

            Console.WriteLine("\n Strategy 4: Priority-Based (Server2 > Server1 > Server3)");
            var priorities = new Dictionary<string, int>
            {
                {"Server1", 5},
                {"Server2", 10},
                {"Server3", 3}
            };
            var resolver4 = ConflictResolver.CreatePriorityResolver(priorities);
            var result4 = resolver4.ResolveConflict(operations);

            Console.WriteLine("\n Strategy 5: Causality-Based");
            var resolver5 = ConflictResolver.CreateCausalityResolver();
            var result5 = resolver5.ResolveConflict(operations);

            // Test conflict detection
            Console.WriteLine("\n Testing Conflict Detection:");
            var clock1 = new Dictionary<string, int> { {"Server1", 5}, {"Server2", 2} };
            var clock2 = new Dictionary<string, int> { {"Server1", 3}, {"Server2", 6} };
            var clock3 = new Dictionary<string, int> { {"Server1", 5}, {"Server2", 6} };

            Console.WriteLine($"Clock1 vs Clock2: {(ConflictResolver.IsConflict(clock1, clock2) ? "CONFLICT (concurrent)" : "No conflict")}");
            Console.WriteLine($"Clock1 vs Clock3: {(ConflictResolver.IsConflict(clock1, clock3) ? "CONFLICT" : "No conflict (Clock3 happened after)")}");

            Console.WriteLine("\n✅ Conflict Resolution Test Complete!");
        }
    }
}