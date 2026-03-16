using Grpc.Net.Client;
using CalculatorServer;
using Shared;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CalculatorClient
{
    class Program
    {
        private static SmartClient _smartClient = null!;
        private static VectorClock _clientClock = null!;
        private static string _clientId = null!;
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string DASHBOARD_URL = "http://localhost:8080";

        static async Task Main(string[] args)
        {
            _clientId = args.Length > 0 ? args[0] : $"Client-{Guid.NewGuid().ToString().Substring(0, 8)}";
            _clientClock = new VectorClock(_clientId);
            _smartClient = new SmartClient(_clientId, maxRetries: 3, retryDelayMs: 1000);

            
            Console.WriteLine("SMART DISTRIBUTED CALCULATOR CLIENT");
            Console.WriteLine($" Client ID: {_clientId}");
            Console.WriteLine($" Auto-Retry: Enabled (3 attempts)");
            Console.WriteLine($" Load Balancing: Round-Robin");
            Console.WriteLine($" Dashboard: {DASHBOARD_URL}");
            Console.WriteLine();

            while (true)
            {
                try
                {
                    Console.WriteLine("\n" + new string('═', 70));
                    Console.WriteLine("CALCULATOR MENU");
                    Console.WriteLine(new string('═', 70));
                    
                    Console.WriteLine("\n Load Balancing Strategy:");
                    Console.WriteLine("  1. Round Robin (default)");
                    Console.WriteLine("  2. Random");
                    Console.WriteLine("  3. Least Loaded");
                    Console.WriteLine("  4. Leader Only");
                    Console.Write("Select strategy (or press Enter for current): ");
                    
                    string? strategyChoice = Console.ReadLine();
                    if (!string.IsNullOrEmpty(strategyChoice) && int.TryParse(strategyChoice, out int strategy))
                    {
                        var selectedStrategy = strategy switch
                        {
                            1 => LoadBalancer.LoadBalancingStrategy.RoundRobin,
                            2 => LoadBalancer.LoadBalancingStrategy.Random,
                            3 => LoadBalancer.LoadBalancingStrategy.LeastLoaded,
                            4 => LoadBalancer.LoadBalancingStrategy.LeaderOnly,
                            _ => LoadBalancer.LoadBalancingStrategy.RoundRobin
                        };
                        _smartClient.SetLoadBalancingStrategy(selectedStrategy);
                    }

                    Console.WriteLine("\n Select operation:");
                    Console.WriteLine("  1. Square (x²)");
                    Console.WriteLine("  2. Cube (x³)");
                    Console.WriteLine("  3. SlowMultiply (x × y)");
                    Console.WriteLine("  q. Quit");
                    Console.Write("Enter choice: ");
                    
                    string? opChoice = Console.ReadLine();
                    if (opChoice?.ToLower() == "q") break;

                    if (!int.TryParse(opChoice, out int operation) || operation < 1 || operation > 3)
                    {
                        Console.WriteLine("❌ Invalid operation!");
                        continue;
                    }

                    if (operation == 3)
                    {
                        Console.Write("\n Enter first number: ");
                        if (!double.TryParse(Console.ReadLine(), out double number1))
                        {
                            Console.WriteLine("❌ Invalid number!");
                            continue;
                        }

                        Console.Write(" Enter second number: ");
                        if (!double.TryParse(Console.ReadLine(), out double number2))
                        {
                            Console.WriteLine("❌ Invalid number!");
                            continue;
                        }

                        await PerformSmartOperation(operation, number1, number2);
                    }
                    else
                    {
                        Console.Write("\n Enter a number: ");
                        if (!double.TryParse(Console.ReadLine(), out double number))
                        {
                            Console.WriteLine("❌ Invalid number!");
                            continue;
                        }

                        await PerformSmartOperation(operation, number);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
            }

            Console.WriteLine("\n Goodbye!");
        }

        static async Task PerformSmartOperation(int operation, double number1, double number2 = 0)
        {
            string opName = operation == 1 ? "Square" : operation == 2 ? "Cube" : "SlowMultiply";
            
            Console.WriteLine($"\n Executing {opName} with Smart Client...");
            _clientClock.Increment();

            var result = await _smartClient.ExecuteOperation<CalculationResponse>(
                async (serverUrl) =>
                {
                    using var channel = GrpcChannel.ForAddress(serverUrl);
                    var client = new Calculator.CalculatorClient(channel);

                    if (operation == 1)
                    {
                        var req = new CalculationRequest
                        {
                            Number = number1,
                            ClientId = _clientId,
                            VectorClock = { _clientClock.GetClock() }
                        };
                        return await client.SquareAsync(req);
                    }
                    else if (operation == 2)
                    {
                        var req = new CalculationRequest
                        {
                            Number = number1,
                            ClientId = _clientId,
                            VectorClock = { _clientClock.GetClock() }
                        };
                        return await client.CubeAsync(req);
                    }
                    else
                    {
                        var req = new MultiplyRequest
                        {
                            Number1 = number1,
                            Number2 = number2,
                            ClientId = _clientId,
                            VectorClock = { _clientClock.GetClock() }
                        };
                        return await client.SlowMultiplyAsync(req);
                    }
                },
                (response) => response.IsSuccess,
                (response) => response.ErrorMessage
            );

            if (result.Success)
            {
                var response = (CalculationResponse)result.Result!;
                Console.WriteLine($"\n✅ SUCCESS after {result.AttemptCount} attempt(s)!");
                Console.WriteLine($" Result: {response.Result}");
                Console.WriteLine($" Server: {result.ServerId}");
                
                _clientClock.Merge(response.VectorClock);
                Console.WriteLine($" Vector Clock: {_clientClock}");
            }
            else
            {
                Console.WriteLine($"\n❌ ALL ATTEMPTS FAILED!");
                Console.WriteLine($" Errors:");
                foreach (var error in result.ErrorMessages)
                {
                    Console.WriteLine($"   - {error}");
                }
            }
        }
    }
}