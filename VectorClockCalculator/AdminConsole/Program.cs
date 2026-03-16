using Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdminConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("DISTRIBUTED SYSTEMS ADMIN CONSOLE");
            Console.WriteLine();
            Console.WriteLine("Control network partitions, leader election, and 2PC");
            Console.WriteLine();

            LeaderElection.Initialize();

            while (true)
            {
                Console.WriteLine("\n" + new string('─', 60));
                Console.WriteLine("MAIN MENU:");
                Console.WriteLine("  1. Network Partition Management");
                Console.WriteLine("  2. Leader Election Management");
                Console.WriteLine("  3. Test Two-Phase Commit");
                Console.WriteLine("  4. Show System Status");
                Console.WriteLine("  q. Quit");
                Console.WriteLine(new string('─', 60));
                Console.Write("\nEnter command: ");

                string? input = Console.ReadLine();
                
                if (input?.ToLower() == "q") break;

                switch (input)
                {
                    case "1":
                        await NetworkPartitionMenu();
                        break;
                    case "2":
                        LeaderElectionMenu();
                        break;
                    case "3":
                        await TestTwoPhaseCommit();
                        break;
                    case "4":
                        ShowSystemStatus();
                        break;
                    default:
                        Console.WriteLine("❌ Invalid command!");
                        break;
                }
            }

            Console.WriteLine("\n Admin console closed.");
        }

        static async Task NetworkPartitionMenu()
        {
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine("NETWORK PARTITION MANAGEMENT");
            Console.WriteLine(new string('═', 60));
            Console.WriteLine("  1. Partition Server1");
            Console.WriteLine("  2. Partition Server2");
            Console.WriteLine("  3. Partition Server3");
            Console.WriteLine("  4. Reconnect Server1");
            Console.WriteLine("  5. Reconnect Server2");
            Console.WriteLine("  6. Reconnect Server3");
            Console.WriteLine("  7. Show partition status");
            Console.WriteLine("  0. Back");
            Console.Write("\nEnter command: ");

            string? input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    NetworkPartition.PartitionNode("Server1");
                    break;
                case "2":
                    NetworkPartition.PartitionNode("Server2");
                    break;
                case "3":
                    NetworkPartition.PartitionNode("Server3");
                    break;
                case "4":
                    NetworkPartition.ReconnectNode("Server1");
                    break;
                case "5":
                    NetworkPartition.ReconnectNode("Server2");
                    break;
                case "6":
                    NetworkPartition.ReconnectNode("Server3");
                    break;
                case "7":
                    ShowPartitionStatus();
                    break;
            }

            await Task.CompletedTask;
        }

        static void LeaderElectionMenu()
        {
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine("LEADER ELECTION MANAGEMENT");
            Console.WriteLine(new string('═', 60));
            Console.WriteLine("  1. Show current leader");
            Console.WriteLine("  2. Trigger new election");
            Console.WriteLine("  3. Simulate leader failure (Server1)");
            Console.WriteLine("  4. Simulate leader failure (Server2)");
            Console.WriteLine("  5. Simulate leader failure (Server3)");
            Console.WriteLine("  6. Restore Server1");
            Console.WriteLine("  7. Restore Server2");
            Console.WriteLine("  8. Restore Server3");
            Console.WriteLine("  0. Back");
            Console.Write("\nEnter command: ");

            string? input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    var leader = LeaderElection.GetLeader();
                    Console.WriteLine($"\n Current Leader: {leader ?? "None"}");
                    break;
                case "2":
                    LeaderElection.ElectNewLeader();
                    break;
                case "3":
                    LeaderElection.LeaderFailed("Server1");
                    break;
                case "4":
                    LeaderElection.LeaderFailed("Server2");
                    break;
                case "5":
                    LeaderElection.LeaderFailed("Server3");
                    break;
                case "6":
                    LeaderElection.RestoreNode("Server1");
                    break;
                case "7":
                    LeaderElection.RestoreNode("Server2");
                    break;
                case "8":
                    LeaderElection.RestoreNode("Server3");
                    break;
            }
        }

        static async Task TestTwoPhaseCommit()
        {
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine("TWO-PHASE COMMIT TEST");
            Console.WriteLine(new string('═', 60));

            var participants = new List<string> { "Server1", "Server2", "Server3" };
            
            Console.WriteLine("\n Starting distributed transaction...");
            
            bool success = await TwoPhaseCommit.ExecuteTransaction(participants, async () =>
            {
                Console.WriteLine("\n Executing distributed operation...");
                await Task.Delay(1000);
                Console.WriteLine("✅ Operation completed");
            });

            if (success)
            {
                Console.WriteLine("\n Transaction completed successfully!");
            }
            else
            {
                Console.WriteLine("\n❌ Transaction failed and was rolled back.");
            }
        }

        static void ShowSystemStatus()
        {
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine("SYSTEM STATUS");
            Console.WriteLine(new string('═', 60));

            // Leader status
            var leader = LeaderElection.GetLeader();
            Console.WriteLine($"\n Current Leader: {leader ?? "None"}");

            // Partition status
            Console.WriteLine("\n PARTITION STATUS:");
            var status = NetworkPartition.GetPartitionStatus();
            
            var allNodes = LeaderElection.GetAllNodes();
            foreach (var node in allNodes)
            {
                bool isPartitioned = status.ContainsKey(node) && status[node];
                bool isLeader = node == leader;
                
                string statusSymbol = isPartitioned ? "🔴 PARTITIONED" : "🟢 CONNECTED";
                string leaderSymbol = isLeader ? " " : "";

                Console.WriteLine($"  {node}: {statusSymbol}{leaderSymbol}");
            }

            Console.WriteLine();
        }

        static void ShowPartitionStatus()
        {
            Console.WriteLine("\n CURRENT PARTITION STATUS:");
            var status = NetworkPartition.GetPartitionStatus();
            
            if (status.Count == 0)
            {
                Console.WriteLine("  ✅ All servers are connected");
            }
            else
            {
                foreach (var kvp in status)
                {
                    string symbol = kvp.Value ? "🔴 PARTITIONED" : "🟢 CONNECTED";
                    Console.WriteLine($"  {kvp.Key}: {symbol}");
                }
            }
        }
    }
}