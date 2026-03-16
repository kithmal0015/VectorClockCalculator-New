using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CalculatorServer
{
    public class DashboardReporter
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string DASHBOARD_URL = "http://localhost:8080";

        public class OperationReport
        {
            public DateTime Timestamp { get; set; }
            public string ClientId { get; set; } = "";
            public string ServerId { get; set; } = "";
            public string Operation { get; set; } = "";
            public double Input1 { get; set; }
            public double Input2 { get; set; }
            public double Result { get; set; }
            public bool Success { get; set; }
            public string ErrorMessage { get; set; } = "";
            public Dictionary<string, int> VectorClock { get; set; } = new();
            public int RetryCount { get; set; } = 0;
        }

        public static async Task ReportOperation(
            string serverId,
            string clientId,
            string operation,
            double input1,
            double input2,
            double result,
            bool success,
            string errorMessage,
            Dictionary<string, int> vectorClock)
        {
            try
            {
                var report = new OperationReport
                {
                    Timestamp = DateTime.Now,
                    ClientId = clientId,
                    ServerId = serverId,
                    Operation = operation,
                    Input1 = input1,
                    Input2 = input2,
                    Result = result,
                    Success = success,
                    ErrorMessage = errorMessage,
                    VectorClock = vectorClock,
                    RetryCount = 0
                };

                await _httpClient.PostAsJsonAsync($"{DASHBOARD_URL}/api/operation", report);
            }
            catch (Exception ex)
            {
                // Silently fail - dashboard might not be running
                Console.WriteLine($" Could not report to dashboard: {ex.Message}");
            }
        }

        public static async Task ReportVectorClock(string serverId, Dictionary<string, int> vectorClock)
        {
            try
            {
                await _httpClient.PostAsJsonAsync($"{DASHBOARD_URL}/api/vector-clock", new
                {
                    ServerId = serverId,
                    VectorClock = vectorClock
                });
            }
            catch
            {
                // Silently fail
            }
        }
    }
}