using CalculatorServer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Shared;
using System;

namespace CalculatorServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string serverId = args.Length > 0 ? args[0] : "Server1";
            string port = args.Length > 1 ? args[1] : "5000";

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.WithProperty("ServerID", serverId)
                .Enrich.WithProperty("Port", port)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServerID}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    $"logs/{serverId}-.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ServerID}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information($"{serverId,-38}");
                Log.Information($"Port: {port}");
                Log.Information($"Started: {DateTime.Now}");

                LeaderElection.Initialize();
                
                CreateHostBuilder(args, serverId, port).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Server terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args, string serverId, string port) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddGrpc();
                        services.AddSingleton(new CalculatorService(serverId));
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGrpcService<CalculatorService>();
                        });
                    });

                    webBuilder.UseUrls($"http://localhost:{port}");
                });
    }
}