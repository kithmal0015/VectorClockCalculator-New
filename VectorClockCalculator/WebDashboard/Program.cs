using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebDashboard;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddSingleton<DashboardDataService>();
builder.Services.AddSingleton<DashboardCalculatorService>();
builder.Services.AddSingleton<ServerHealthMonitor>();
builder.Services.AddControllers();

var app = builder.Build();

// Initialize leader election
LeaderElection.Initialize();

// Start health monitor
var healthMonitor = app.Services.GetRequiredService<ServerHealthMonitor>();
healthMonitor.Start();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.MapHub<DashboardHub>("/dashboardHub");
app.MapControllers();

// API Endpoints
app.MapGet("/api/status", (DashboardDataService dataService) => 
{
    return Results.Ok(dataService.GetSystemStatus());
});

app.MapGet("/api/history", (DashboardDataService dataService, int count = 50) => 
{
    return Results.Ok(dataService.GetOperationHistory(count));
});

app.MapPost("/api/calculate", async (
    DashboardCalculatorService calcService,
    DashboardDataService dataService,
    IHubContext<DashboardHub> hubContext,
    CalculationRequest request) =>
{
    var result = await calcService.PerformCalculation(
        request.ServerUrl,
        request.Operation,
        request.Number1,
        request.Number2
    );

    // Broadcast to all clients
    await hubContext.Clients.All.SendAsync("OperationCompleted", result);
    
    return Results.Ok(result);
});

app.MapPost("/api/calculate-auto", async (
    DashboardCalculatorService calcService,
    DashboardDataService dataService,
    IHubContext<DashboardHub> hubContext,
    CalculationRequest request) =>
{
    var result = await calcService.PerformCalculationWithAutoFailover(
        request.Operation,
        request.Number1,
        request.Number2
    );

    // Broadcast to all clients
    await hubContext.Clients.All.SendAsync("OperationCompleted", result);
    
    return Results.Ok(result);
});

// Endpoint to receive operation reports from servers
app.MapPost("/api/operation", async (
    DashboardDataService dataService,
    IHubContext<DashboardHub> hubContext,
    OperationHistory operation) =>
{
    dataService.AddOperation(operation);
    
    // Broadcast to all connected dashboard clients
    await hubContext.Clients.All.SendAsync("OperationCompleted", operation);
    
    return Results.Ok();
});

// Endpoint to receive vector clock updates from servers
app.MapPost("/api/vector-clock", async (
    DashboardDataService dataService,
    IHubContext<DashboardHub> hubContext,
    VectorClockUpdate update) =>
{
    dataService.UpdateVectorClock(update.ServerId, update.VectorClock);
    await hubContext.Clients.All.SendAsync("VectorClockUpdated", update.ServerId, update.VectorClock);
    return Results.Ok();
});

app.MapPost("/api/partition/{serverId}", async (
    string serverId,
    IHubContext<DashboardHub> hubContext) =>
{
    NetworkPartition.PartitionNode(serverId);
    await hubContext.Clients.All.SendAsync("ServerStatusChanged", serverId, false, true);
    return Results.Ok(new { message = $"{serverId} partitioned" });
});

app.MapPost("/api/reconnect/{serverId}", async (
    string serverId,
    IHubContext<DashboardHub> hubContext) =>
{
    NetworkPartition.ReconnectNode(serverId);
    LeaderElection.RestoreNode(serverId);
    await hubContext.Clients.All.SendAsync("ServerStatusChanged", serverId, true, false);
    return Results.Ok(new { message = $"{serverId} reconnected" });
});

app.MapPost("/api/clear-history", async (
    DashboardDataService dataService,
    IHubContext<DashboardHub> hubContext) =>
{
    dataService.ClearHistory();
    await hubContext.Clients.All.SendAsync("HistoryCleared");
    return Results.Ok();
});

app.MapGet("/api/servers", (ServerHealthMonitor healthMonitor) =>
{
    return Results.Ok(healthMonitor.GetServerStatus());
});

System.Console.WriteLine("DISTRIBUTED CALCULATOR DASHBOARD");
System.Console.WriteLine();
System.Console.WriteLine("Dashboard URL: http://localhost:8080");
System.Console.WriteLine("Health monitoring: Active");
System.Console.WriteLine("SignalR hub: Ready");
System.Console.WriteLine("gRPC client: Configured");
System.Console.WriteLine("Receiving reports from all clients");
System.Console.WriteLine();

app.Run("http://localhost:8080");

// Request/Response Models - Must be at the END after all top-level statements

public class CalculationRequest
{
    public string ServerUrl { get; set; } = "";
    public string Operation { get; set; } = "";
    public double Number1 { get; set; }
    public double Number2 { get; set; }
}

public class VectorClockUpdate
{
    public string ServerId { get; set; } = "";
    public Dictionary<string, int> VectorClock { get; set; } = new();
}