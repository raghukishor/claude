using Microsoft.Extensions.Logging;
using Rbac.Shared.Models.ControlPlane;

namespace Rbac.SyncService.Processors;

/// <summary>
/// Processes role definition changes.
/// Currently a no-op as we moved to runtime evaluation.
/// </summary>
public class RoleDefinitionProcessor
{
    private readonly ILogger<RoleDefinitionProcessor> _logger;

    public RoleDefinitionProcessor(
        ILogger<RoleDefinitionProcessor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Processes a role definition change by updating all affected EffectiveAccess documents.
    /// </summary>
    public Task ProcessAsync(RoleDefinition roleDefinition)
    {
        _logger.LogInformation(
            "Processing role definition {RoleDefinitionId} with changeId {ChangeId} (No-op)",
            roleDefinition.Id,
            roleDefinition.Version?.ChangeId);

        return Task.CompletedTask;
    }
}
