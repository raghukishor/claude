using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rbac.Shared.CosmosDb;
using Rbac.Shared.Models.ControlPlane;
using Rbac.SyncService.Processors;

namespace Rbac.SyncService.Workers;

/// <summary>
/// Background worker that monitors the RoleAssignments container for changes.
/// </summary>
public class RoleAssignmentChangeFeedWorker : BackgroundService
{
    private readonly ICosmosDbClient _controlPlaneClient;
    private readonly RoleAssignmentProcessor _processor;
    private readonly ILogger<RoleAssignmentChangeFeedWorker> _logger;
    private ChangeFeedProcessor? _changeFeedProcessor;

    public RoleAssignmentChangeFeedWorker(
        ICosmosDbClient controlPlaneClient,
        RoleAssignmentProcessor processor,
        ILogger<RoleAssignmentChangeFeedWorker> logger)
    {
        _controlPlaneClient = controlPlaneClient;
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting RoleAssignment Change Feed Worker...");

        try
        {
            var sourceContainer = _controlPlaneClient.GetContainer(ContainerNames.RoleAssignments);
            var leaseContainer = _controlPlaneClient.GetContainer(ContainerNames.SyncLeases);

            _changeFeedProcessor = sourceContainer
                .GetChangeFeedProcessorBuilder<RoleAssignment>(
                    "RoleAssignmentProcessor",
                    HandleChangesAsync)
                .WithInstanceName(Environment.MachineName)
                .WithLeaseContainer(leaseContainer)
                .WithStartTime(DateTime.UtcNow.AddHours(-24)) // Start from 24 hours ago for initial run
                .Build();

            await _changeFeedProcessor.StartAsync();
            _logger.LogInformation("RoleAssignment Change Feed Processor started");

            // Keep running until cancellation
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RoleAssignment Change Feed Worker stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RoleAssignment Change Feed Worker");
            throw;
        }
    }

    private async Task HandleChangesAsync(
        ChangeFeedProcessorContext context,
        IReadOnlyCollection<RoleAssignment> changes,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Processing {Count} role assignment changes from lease {LeaseToken}",
            changes.Count,
            context.LeaseToken);

        foreach (var assignment in changes)
        {
            try
            {
                await _processor.ProcessAsync(assignment);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process role assignment {AssignmentId}",
                    assignment.Id);
                // Continue processing other changes
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_changeFeedProcessor != null)
        {
            await _changeFeedProcessor.StopAsync();
            _logger.LogInformation("RoleAssignment Change Feed Processor stopped");
        }

        await base.StopAsync(cancellationToken);
    }
}
