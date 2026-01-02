using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rbac.Shared.CosmosDb;
using Rbac.Shared.Models.ControlPlane;
using Rbac.SyncService.Processors;

namespace Rbac.SyncService.Workers;

/// <summary>
/// Background worker that monitors the RoleDefinitions container for changes.
/// </summary>
public class RoleDefinitionChangeFeedWorker : BackgroundService
{
    private readonly ICosmosDbClient _controlPlaneClient;
    private readonly RoleDefinitionProcessor _processor;
    private readonly ILogger<RoleDefinitionChangeFeedWorker> _logger;
    private ChangeFeedProcessor? _changeFeedProcessor;

    public RoleDefinitionChangeFeedWorker(
        ICosmosDbClient controlPlaneClient,
        RoleDefinitionProcessor processor,
        ILogger<RoleDefinitionChangeFeedWorker> logger)
    {
        _controlPlaneClient = controlPlaneClient;
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting RoleDefinition Change Feed Worker...");

        try
        {
            var sourceContainer = _controlPlaneClient.GetContainer(ContainerNames.RoleDefinitions);
            var leaseContainer = _controlPlaneClient.GetContainer(ContainerNames.SyncLeases);

            _changeFeedProcessor = sourceContainer
                .GetChangeFeedProcessorBuilder<RoleDefinition>(
                    "RoleDefinitionProcessor",
                    HandleChangesAsync)
                .WithInstanceName(Environment.MachineName)
                .WithLeaseContainer(leaseContainer)
                .WithStartTime(DateTime.UtcNow.AddHours(-24))
                .Build();

            await _changeFeedProcessor.StartAsync();
            _logger.LogInformation("RoleDefinition Change Feed Processor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RoleDefinition Change Feed Worker stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RoleDefinition Change Feed Worker");
            throw;
        }
    }

    private async Task HandleChangesAsync(
        ChangeFeedProcessorContext context,
        IReadOnlyCollection<RoleDefinition> changes,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Processing {Count} role definition changes from lease {LeaseToken}",
            changes.Count,
            context.LeaseToken);

        foreach (var definition in changes)
        {
            try
            {
                await _processor.ProcessAsync(definition);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process role definition {DefinitionId}",
                    definition.Id);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_changeFeedProcessor != null)
        {
            await _changeFeedProcessor.StopAsync();
            _logger.LogInformation("RoleDefinition Change Feed Processor stopped");
        }

        await base.StopAsync(cancellationToken);
    }
}
