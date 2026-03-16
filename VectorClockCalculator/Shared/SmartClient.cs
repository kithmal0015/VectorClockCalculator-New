using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Shared
{
    public class SmartClient
    {
        private readonly string _clientId;
        private readonly LoadBalancer _loadBalancer;
        private readonly int _maxRetries;
        private readonly int _retryDelayMs;
        private readonly bool _useSessionAffinity;
        private string? _affinityServer;

        public SmartClient(
            string clientId,
            int maxRetries = 3,
            int retryDelayMs = 1000,
            bool useSessionAffinity = false)
        {
            _clientId = clientId;
            _maxRetries = maxRetries;
            _retryDelayMs = retryDelayMs;
            _useSessionAffinity = useSessionAffinity;
            _loadBalancer = new LoadBalancer();
        }

        public async Task<OperationResult> ExecuteOperation<T>(
            Func<string, Task<T>> operation,
            Func<T, bool> isSuccess,
            Func<T, string> getErrorMessage) where T : class
        {
            var attemptedServers = new List<string>();
            var errors = new List<string>();

            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    var server = GetNextServer(attemptedServers);
                    
                    if (server == null)
                    {
                        Console.WriteLine($"❌ [{_clientId}] No more servers available");
                        break;
                    }

                    attemptedServers.Add(server.ServerId);
                    Console.WriteLine($" [{_clientId}] Attempt {attempt}/{_maxRetries} on {server.ServerId}");

                    _loadBalancer.IncrementLoad(server.ServerId);

                    try
                    {
                        var result = await operation(server.Url);

                        if (isSuccess(result))
                        {
                            Console.WriteLine($"✅ [{_clientId}] Success on {server.ServerId}");
                            
                            if (_useSessionAffinity)
                            {
                                _affinityServer = server.ServerId;
                            }

                            return new OperationResult
                            {
                                Success = true,
                                ServerId = server.ServerId,
                                AttemptCount = attempt,
                                Result = result
                            };
                        }
                        else
                        {
                            var error = getErrorMessage(result);
                            Console.WriteLine($"❌ [{_clientId}] {server.ServerId} failed: {error}");
                            errors.Add($"{server.ServerId}: {error}");
                        }
                    }
                    finally
                    {
                        _loadBalancer.DecrementLoad(server.ServerId);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ [{_clientId}] Exception: {ex.Message}");
                    errors.Add($"Attempt {attempt}: {ex.Message}");
                }

                if (attempt < _maxRetries)
                {
                    Console.WriteLine($" [{_clientId}] Waiting {_retryDelayMs}ms before retry...");
                    await Task.Delay(_retryDelayMs);
                }
            }

            Console.WriteLine($"❌ [{_clientId}] All {_maxRetries} attempts failed");
            return new OperationResult
            {
                Success = false,
                ErrorMessages = errors,
                AttemptCount = _maxRetries
            };
        }

        private LoadBalancer.ServerEndpoint? GetNextServer(List<string> attemptedServers)
        {
            if (_useSessionAffinity && !string.IsNullOrEmpty(_affinityServer))
            {
                var affinityServer = _loadBalancer.GetAllServers()
                    .FirstOrDefault(s => s.ServerId == _affinityServer && 
                                        !NetworkPartition.IsPartitioned(s.ServerId));
                
                if (affinityServer != null)
                {
                    return affinityServer;
                }
            }

            for (int i = 0; i < 10; i++)
            {
                var server = _loadBalancer.GetNextServer();
                
                if (server == null)
                    return null;

                if (!attemptedServers.Contains(server.ServerId))
                    return server;
            }

            return null;
        }

        public void SetLoadBalancingStrategy(LoadBalancer.LoadBalancingStrategy strategy)
        {
            _loadBalancer.SetStrategy(strategy);
        }

        public class OperationResult
        {
            public bool Success { get; set; }
            public string ServerId { get; set; } = "";
            public int AttemptCount { get; set; }
            public object? Result { get; set; }
            public List<string> ErrorMessages { get; set; } = new();
        }
    }
}