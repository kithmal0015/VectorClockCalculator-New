using Grpc.Net.Client;
using CalculatorServer;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebDashboard
{
    public class DashboardCalculatorService
    {
        private readonly VectorClock _dashboardClock;
        private readonly DashboardDataService _dataService;
        private const string DASHBOARD_ID = "Dashboard";
        private readonly List<string> _serverUrls = new()
        {
            "http://localhost:5000",
            "http://localhost:5001",
            "http://localhost:5002"
        };

        public DashboardCalculatorService(DashboardDataService dataService)
        {
            _dashboardClock = new VectorClock(DASHBOARD_ID);
            _dataService = dataService;
        }

        public async Task<OperationHistory> PerformCalculation(
            string serverUrl, 
            string operation, 
            double number1, 
            double number2 = 0)
        {
            var serverId = GetServerIdFromUrl(serverUrl);
            var startTime = DateTime.Now;

            // Check if server is available
            if (NetworkPartition.IsPartitioned(serverId))
            {
                Console.WriteLine($"🔴 [{serverId}] Server is partitioned, trying failover...");
                
                // Try automatic failover to another server
                return await PerformCalculationWithAutoFailover(operation, number1, number2);
            }

            // Increment clock before operation
            _dashboardClock.Increment();

            try
            {
                using var channel = GrpcChannel.ForAddress(serverUrl);
                var client = new Calculator.CalculatorClient(channel);

                CalculationResponse response;

                switch (operation.ToLower())
                {
                    case "square":
                        var squareRequest = new CalculatorServer.CalculationRequest
                        {
                            Number = number1,
                            ClientId = DASHBOARD_ID,
                            VectorClock = { _dashboardClock.GetClock() }
                        };
                        response = await client.SquareAsync(squareRequest);
                        break;

                    case "cube":
                        var cubeRequest = new CalculatorServer.CalculationRequest
                        {
                            Number = number1,
                            ClientId = DASHBOARD_ID,
                            VectorClock = { _dashboardClock.GetClock() }
                        };
                        response = await client.CubeAsync(cubeRequest);
                        break;

                    case "multiply":
                        var multiplyRequest = new MultiplyRequest
                        {
                            Number1 = number1,
                            Number2 = number2,
                            ClientId = DASHBOARD_ID,
                            VectorClock = { _dashboardClock.GetClock() }
                        };
                        response = await client.SlowMultiplyAsync(multiplyRequest);
                        break;

                    default:
                        throw new ArgumentException($"Unknown operation: {operation}");
                }

                // Merge response clock
                _dashboardClock.Merge(response.VectorClock);

                // Check if operation was successful
                if (!response.IsSuccess)
                {
                    Console.WriteLine($"❌ [{serverId}] Operation failed, trying automatic failover...");
                    // Automatically failover to another server
                    return await PerformCalculationWithAutoFailover(operation, number1, number2);
                }

                var operationHistory = new OperationHistory
                {
                    Timestamp = startTime,
                    ClientId = DASHBOARD_ID,
                    ServerId = serverId,
                    Operation = operation,
                    Input1 = number1,
                    Input2 = number2,
                    Result = response.Result,
                    Success = response.IsSuccess,
                    ErrorMessage = response.ErrorMessage,
                    VectorClock = _dashboardClock.GetClock()
                };

                return operationHistory;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [{serverId}] Connection/Error: {ex.Message}");
                Console.WriteLine($" Attempting automatic failover...");
                
                // Automatically failover to another server
                return await PerformCalculationWithAutoFailover(operation, number1, number2);
            }
        }

        public async Task<OperationHistory> PerformCalculationWithAutoFailover(
            string operation,
            double number1,
            double number2 = 0)
        {
            Console.WriteLine($"\n AUTO-FAILOVER ACTIVATED");
            Console.WriteLine($"Operation: {operation}, Input: {number1}, {number2}");
            
            // Try servers in order until one succeeds
            var availableServers = _serverUrls
                .Select((url, index) => new { Url = url, ServerId = $"Server{index + 1}" })
                .Where(s => !NetworkPartition.IsPartitioned(s.ServerId))
                .ToList();

            if (!availableServers.Any())
            {
                Console.WriteLine("❌ No available servers for failover");
                return new OperationHistory
                {
                    Timestamp = DateTime.Now,
                    ClientId = DASHBOARD_ID,
                    ServerId = "None",
                    Operation = operation,
                    Input1 = number1,
                    Input2 = number2,
                    Result = 0,
                    Success = false,
                    ErrorMessage = "All servers are unavailable or partitioned",
                    VectorClock = _dashboardClock.GetClock()
                };
            }

            Console.WriteLine($"Available servers for failover: {string.Join(", ", availableServers.Select(s => s.ServerId))}");

            // Try each available server
            foreach (var server in availableServers)
            {
                Console.WriteLine($"Attempting operation on {server.ServerId}...");
                
                try
                {
                    _dashboardClock.Increment();
                    
                    using var channel = GrpcChannel.ForAddress(server.Url);
                    var client = new Calculator.CalculatorClient(channel);

                    CalculationResponse response;

                    switch (operation.ToLower())
                    {
                        case "square":
                            var squareRequest = new CalculatorServer.CalculationRequest
                            {
                                Number = number1,
                                ClientId = DASHBOARD_ID,
                                VectorClock = { _dashboardClock.GetClock() }
                            };
                            response = await client.SquareAsync(squareRequest);
                            break;

                        case "cube":
                            var cubeRequest = new CalculatorServer.CalculationRequest
                            {
                                Number = number1,
                                ClientId = DASHBOARD_ID,
                                VectorClock = { _dashboardClock.GetClock() }
                            };
                            response = await client.CubeAsync(cubeRequest);
                            break;

                        case "multiply":
                            var multiplyRequest = new MultiplyRequest
                            {
                                Number1 = number1,
                                Number2 = number2,
                                ClientId = DASHBOARD_ID,
                                VectorClock = { _dashboardClock.GetClock() }
                            };
                            response = await client.SlowMultiplyAsync(multiplyRequest);
                            break;

                        default:
                            throw new ArgumentException($"Unknown operation: {operation}");
                    }

                    if (response.IsSuccess)
                    {
                        _dashboardClock.Merge(response.VectorClock);
                        
                        Console.WriteLine($"✅ SUCCESS on {server.ServerId} (Failover)");
                        
                        return new OperationHistory
                        {
                            Timestamp = DateTime.Now,
                            ClientId = DASHBOARD_ID,
                            ServerId = server.ServerId,
                            Operation = operation,
                            Input1 = number1,
                            Input2 = number2,
                            Result = response.Result,
                            Success = true,
                            ErrorMessage = $"✅ Succeeded on {server.ServerId} (Auto-Failover)",
                            VectorClock = _dashboardClock.GetClock()
                        };
                    }
                    else
                    {
                        Console.WriteLine($"❌ {server.ServerId} returned failure: {response.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ {server.ServerId} failed: {ex.Message}");
                }
            }

            // All servers failed
            Console.WriteLine("❌ ALL SERVERS FAILED during failover");
            return new OperationHistory
            {
                Timestamp = DateTime.Now,
                ClientId = DASHBOARD_ID,
                ServerId = "All Failed",
                Operation = operation,
                Input1 = number1,
                Input2 = number2,
                Result = 0,
                Success = false,
                ErrorMessage = "All failover attempts failed on all available servers",
                VectorClock = _dashboardClock.GetClock()
            };
        }

        private string GetServerIdFromUrl(string url)
        {
            if (url.Contains("5000")) return "Server1";
            if (url.Contains("5001")) return "Server2";
            if (url.Contains("5002")) return "Server3";
            return "Unknown";
        }

        public List<string> GetAvailableServers()
        {
            return _serverUrls
                .Select((url, index) => new { Url = url, ServerId = $"Server{index + 1}" })
                .Where(s => !NetworkPartition.IsPartitioned(s.ServerId))
                .Select(s => s.Url)
                .ToList();
        }
    }
}