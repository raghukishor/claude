using DistributedWorkerService.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Add distributed worker services
builder.Services.AddDistributedWorkerServices(builder.Configuration);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var host = builder.Build();

// Configure health check endpoints (optional, for monitoring)
if (args.Contains("--health-check"))
{
    // Simple health check endpoint for container orchestration
    var healthCheckTask = Task.Run(async () =>
    {
        using var scope = host.Services.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>();
        
        while (!host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested)
        {
            try
            {
                var result = await healthCheckService.CheckHealthAsync();
                Console.WriteLine($"Health Status: {result.Status}");
                
                if (result.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
                {
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Health check failed: {ex.Message}");
                Environment.Exit(1);
            }
            
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    });
}

await host.RunAsync();
