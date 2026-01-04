using Microsoft.Extensions.Logging;
using Rbac.Shared.CosmosDb;
using Rbac.Shared.Models.ControlPlane;

namespace Rbac.SyncService.Processors;

/// <summary>
/// Processes role assignment changes from the change feed.
/// Currently a no-op as we moved to runtime evaluation.
/// </summary>
public class RoleAssignmentProcessor
{
    private readonly ILogger<RoleAssignmentProcessor> _logger;

    public RoleAssignmentProcessor(
        ILogger<RoleAssignmentProcessor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Processes a role assignment change.
    /// </summary>
    public Task ProcessAsync(RoleAssignment assignment)
    {
        _logger.LogInformation(
            "Processing role assignment {AssignmentId} with changeId {ChangeId} (No-op)",
            assignment.Id,
            assignment.Version?.ChangeId);

        return Task.CompletedTask;
    }
}
