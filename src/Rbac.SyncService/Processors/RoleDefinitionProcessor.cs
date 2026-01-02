using Microsoft.Extensions.Logging;
using Rbac.Shared.CosmosDb;
using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.DataPlane;
using Rbac.SyncService.Idempotency;

namespace Rbac.SyncService.Processors;

/// <summary>
/// Processes role definition changes and updates affected EffectiveAccess documents.
/// </summary>
public class RoleDefinitionProcessor
{
    private readonly CosmosDbRepository<EffectiveAccess> _effectiveAccessRepo;
    private readonly DenormalizationEngine _denormalizationEngine;
    private readonly ReplayDetector _replayDetector;
    private readonly ILogger<RoleDefinitionProcessor> _logger;

    public RoleDefinitionProcessor(
        ICosmosDbClient dataPlaneClient,
        DenormalizationEngine denormalizationEngine,
        ReplayDetector replayDetector,
        ILogger<RoleDefinitionProcessor> logger)
    {
        _effectiveAccessRepo = new CosmosDbRepository<EffectiveAccess>(
            dataPlaneClient, ContainerNames.EffectiveAccess);
        _denormalizationEngine = denormalizationEngine;
        _replayDetector = replayDetector;
        _logger = logger;
    }

    /// <summary>
    /// Processes a role definition change by updating all affected EffectiveAccess documents.
    /// </summary>
    public async Task ProcessAsync(RoleDefinition roleDefinition)
    {
        _logger.LogInformation(
            "Processing role definition {RoleDefinitionId} with changeId {ChangeId}",
            roleDefinition.Id,
            roleDefinition.Version?.ChangeId);

        // Find all EffectiveAccess documents that reference this role definition
        var query = "SELECT * FROM c WHERE ARRAY_CONTAINS(c.effectiveRoles, {'roleDefinitionId': @roleDefId}, true)";
        var affectedDocs = await _effectiveAccessRepo.QueryAsync(
            query,
            new Dictionary<string, object> { { "roleDefId", roleDefinition.Id } });

        _logger.LogInformation(
            "Found {Count} EffectiveAccess documents referencing role definition {RoleDefinitionId}",
            affectedDocs.Count,
            roleDefinition.Id);

        foreach (var doc in affectedDocs)
        {
            try
            {
                await UpdateEffectiveAccessAsync(doc, roleDefinition);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to update EffectiveAccess {Id} for role definition {RoleDefinitionId}",
                    doc.Id,
                    roleDefinition.Id);
            }
        }
    }

    private async Task UpdateEffectiveAccessAsync(
        EffectiveAccess effectiveAccess,
        RoleDefinition roleDefinition)
    {
        // Check for replay on each affected role
        var hasChanges = false;

        foreach (var role in effectiveAccess.EffectiveRoles.Where(r => r.RoleDefinitionId == roleDefinition.Id))
        {
            if (_replayDetector.ShouldProcessDefinitionChange(
                roleDefinition.Version?.ChangeId ?? "",
                roleDefinition.Version?.SequenceNumber ?? 0,
                role.SourceVersions))
            {
                hasChanges = true;
                break;
            }
        }

        if (!hasChanges)
        {
            _logger.LogDebug(
                "Skipping replay of role definition {RoleDefinitionId} for EffectiveAccess {Id}",
                roleDefinition.Id,
                effectiveAccess.Id);
            return;
        }

        // Handle delete
        if (roleDefinition.Lifecycle?.IsDeleted == true)
        {
            // Remove all roles that reference this definition
            effectiveAccess.EffectiveRoles.RemoveAll(r => r.RoleDefinitionId == roleDefinition.Id);

            if (effectiveAccess.EffectiveRoles.Count == 0)
            {
                await _effectiveAccessRepo.DeleteAsync(effectiveAccess.Id, effectiveAccess.PrincipalId);
                return;
            }
        }
        else
        {
            // Update the role definition info
            effectiveAccess = _denormalizationEngine.UpdateRoleDefinition(effectiveAccess, roleDefinition);
        }

        await _effectiveAccessRepo.UpsertAsync(effectiveAccess, effectiveAccess.PrincipalId);
    }
}
