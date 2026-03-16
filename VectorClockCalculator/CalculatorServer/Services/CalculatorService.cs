using Grpc.Core;
using Shared;
using System;
using System.Threading.Tasks;

namespace CalculatorServer.Services
{
    public class CalculatorService : Calculator.CalculatorBase
    {
        private readonly VectorClock _vectorClock;
        private readonly string _serverId;
        private readonly Random _random;
        private readonly ErrorSimulator _errorSimulator;
        private int _operationCount = 0;

        public CalculatorService(string serverId = "Server1")
        {
            _serverId = serverId;
            _vectorClock = new VectorClock(_serverId);
            _random = new Random();
            _errorSimulator = new ErrorSimulator(_serverId);
            Console.WriteLine($"✅ {_serverId} initialized with Vector Clock");
        }

        public override async Task<CalculationResponse> Square(
            CalculationRequest request, 
            ServerCallContext context)
        {
            _operationCount++;
            Console.WriteLine($"\n{new string('=', 50)} Operation #{_operationCount} {new string('=', 50)}");
            Console.WriteLine($" [{_serverId}] Received Square request for {request.Number}");
            Console.WriteLine($" From client: {request.ClientId}");

            // CRITICAL: Check for network partition FIRST
            if (NetworkPartition.IsPartitioned(_serverId))
            {
                Console.WriteLine($"🔴 [{_serverId}] REJECTED - Server is partitioned!");
                
                await DashboardReporter.ReportOperation(
                    _serverId, request.ClientId, "Square", 
                    request.Number, 0, 0, false, 
                    "Server is partitioned", 
                    _vectorClock.GetClock()
                );
                
                throw new RpcException(new Status(StatusCode.Unavailable, "Server is partitioned"));
            }
            
            var snapshot = _vectorClock.CreateSnapshot();

            try
            {
                _vectorClock.Merge(request.VectorClock);
                _vectorClock.Increment();

                // Simulate network conditions (delays, timeouts)
                await _errorSimulator.SimulateNetworkConditions();

                int delay = _random.Next(2000, 5000);
                Console.WriteLine($" [{_serverId}] Processing... (delay: {delay}ms)");
                await Task.Delay(delay);

                // Check for errors
                if (_errorSimulator.ShouldFail(request.Number))
                {
                    throw new Exception("Operation failed due to simulated error");
                }

                double result = Math.Pow(request.Number, 2);
                Console.WriteLine($"✅ [{_serverId}] Square({request.Number}) = {result}");

                // Report success to dashboard
                await DashboardReporter.ReportOperation(
                    _serverId, request.ClientId, "Square", 
                    request.Number, 0, result, true, "", 
                    _vectorClock.GetClock()
                );

                return new CalculationResponse
                {
                    Result = result,
                    IsSuccess = true,
                    ErrorMessage = "",
                    VectorClock = { _vectorClock.GetClock() }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [{_serverId}] Error: {ex.Message}");
                _vectorClock.Restore(snapshot);

                // Report failure to dashboard
                await DashboardReporter.ReportOperation(
                    _serverId, request.ClientId, "Square", 
                    request.Number, 0, 0, false, 
                    ex.Message, _vectorClock.GetClock()
                );

                return new CalculationResponse
                {
                    Result = 0,
                    IsSuccess = false,
                    ErrorMessage = $"Operation failed: {ex.Message}",
                    VectorClock = { _vectorClock.GetClock() }
                };
            }
        }

        public override async Task<CalculationResponse> Cube(
            CalculationRequest request, 
            ServerCallContext context)
        {
            _operationCount++;
            Console.WriteLine($"\n{new string('=', 50)} Operation #{_operationCount} {new string('=', 50)}");
            Console.WriteLine($" [{_serverId}] Received Cube request for {request.Number}");
            Console.WriteLine($" From client: {request.ClientId}");

            // CRITICAL: Check for network partition FIRST
            if (NetworkPartition.IsPartitioned(_serverId))
            {
                Console.WriteLine($"🔴 [{_serverId}] REJECTED - Server is partitioned!");
                
                await DashboardReporter.ReportOperation(
                    _serverId, request.ClientId, "Cube", 
                    request.Number, 0, 0, false, 
                    "Server is partitioned", 
                    _vectorClock.GetClock()
                );
                
                throw new RpcException(new Status(StatusCode.Unavailable, "Server is partitioned"));
            }
            
            var snapshot = _vectorClock.CreateSnapshot();

            try
            {
                _vectorClock.Merge(request.VectorClock);
                _vectorClock.Increment();

                // Simulate network conditions
                await _errorSimulator.SimulateNetworkConditions();

                int delay = _random.Next(2000, 5000);
                Console.WriteLine($" [{_serverId}] Processing... (delay: {delay}ms)");
                await Task.Delay(delay);

                // Check for errors
                if (_errorSimulator.ShouldFail(request.Number))
                {
                    throw new Exception("Operation failed due to simulated error");
                }

                double result = Math.Pow(request.Number, 3);
                Console.WriteLine($"✅ [{_serverId}] Cube({request.Number}) = {result}");

                // Report success to dashboard
                await DashboardReporter.ReportOperation(
                    _serverId, request.ClientId, "Cube", 
                    request.Number, 0, result, true, "", 
                    _vectorClock.GetClock()
                );

                return new CalculationResponse
                {
                    Result = result,
                    IsSuccess = true,
                    ErrorMessage = "",
                    VectorClock = { _vectorClock.GetClock() }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [{_serverId}] Error: {ex.Message}");
                _vectorClock.Restore(snapshot);

                // Report failure to dashboard
                await DashboardReporter.ReportOperation(
                    _serverId, request.ClientId, "Cube", 
                    request.Number, 0, 0, false, 
                    ex.Message, _vectorClock.GetClock()
                );

                return new CalculationResponse
                {
                    Result = 0,
                    IsSuccess = false,
                    ErrorMessage = $"Operation failed: {ex.Message}",
                    VectorClock = { _vectorClock.GetClock() }
                };
            }
        }

        public override async Task<CalculationResponse> SlowMultiply(
            MultiplyRequest request, 
            ServerCallContext context)
        {
            _operationCount++;
            Console.WriteLine($"\n{new string('=', 50)} Operation #{_operationCount} {new string('=', 50)}");
            Console.WriteLine($" [{_serverId}] Received SlowMultiply request: {request.Number1} * {request.Number2}");
            Console.WriteLine($" From client: {request.ClientId}");

            // CRITICAL: Check for network partition FIRST
            if (NetworkPartition.IsPartitioned(_serverId))
            {
                Console.WriteLine($"🔴 [{_serverId}] REJECTED - Server is partitioned!");
                
                await DashboardReporter.ReportOperation(
                    _serverId, request.ClientId, "SlowMultiply", 
                    request.Number1, request.Number2, 0, false, 
                    "Server is partitioned", 
                    _vectorClock.GetClock()
                );
                
                throw new RpcException(new Status(StatusCode.Unavailable, "Server is partitioned"));
            }
            
            var snapshot = _vectorClock.CreateSnapshot();

            try
            {
                _vectorClock.Merge(request.VectorClock);
                _vectorClock.Increment();

                // Simulate network conditions
                await _errorSimulator.SimulateNetworkConditions();

                Console.WriteLine($" [{_serverId}] Processing SlowMultiply... (5000ms)");
                await Task.Delay(5000);

                // Check for errors on both inputs
                if (_errorSimulator.ShouldFail(request.Number1) || _errorSimulator.ShouldFail(request.Number2))
                {
                    throw new Exception("Operation failed due to simulated error");
                }

                double result = request.Number1 * request.Number2;
                Console.WriteLine($"✅ [{_serverId}] SlowMultiply({request.Number1} * {request.Number2}) = {result}");

                // Report success to dashboard
                await DashboardReporter.ReportOperation(
                    _serverId, request.ClientId, "SlowMultiply", 
                    request.Number1, request.Number2, result, true, "", 
                    _vectorClock.GetClock()
                );

                return new CalculationResponse
                {
                    Result = result,
                    IsSuccess = true,
                    ErrorMessage = "",
                    VectorClock = { _vectorClock.GetClock() }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [{_serverId}] Error: {ex.Message}");
                _vectorClock.Restore(snapshot);

                // Report failure to dashboard
                await DashboardReporter.ReportOperation(
                    _serverId, request.ClientId, "SlowMultiply", 
                    request.Number1, request.Number2, 0, false, 
                    ex.Message, _vectorClock.GetClock()
                );

                return new CalculationResponse
                {
                    Result = 0,
                    IsSuccess = false,
                    ErrorMessage = $"Operation failed: {ex.Message}",
                    VectorClock = { _vectorClock.GetClock() }
                };
            }
        }
    }
}