using Microsoft.Extensions.Logging;
using Rbac.Shared.CosmosDb;
using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.DataPlane;
using Rbac.SyncService.Idempotency;

namespace Rbac.SyncService.Processors;

/// <summary>
/// Processes role assignment changes from the change feed.
/// </summary>
public class RoleAssignmentProcessor
{
    private readonly CosmosDbRepository<EffectiveAccess> _effectiveAccessRepo;
    private readonly CosmosDbRepository<RoleDefinition> _roleDefinitionRepo;
    private readonly DenormalizationEngine _denormalizationEngine;
    private readonly ReplayDetector _replayDetector;
    private readonly ILogger<RoleAssignmentProcessor> _logger;

    public RoleAssignmentProcessor(
        ICosmosDbClient dataPlaneClient,
        ICosmosDbClient controlPlaneClient,
        DenormalizationEngine denormalizationEngine,
        ReplayDetector replayDetector,
        ILogger<RoleAssignmentProcessor> logger)
    {
        _effectiveAccessRepo = new CosmosDbRepository<EffectiveAccess>(
            dataPlaneClient, ContainerNames.EffectiveAccess);
        _roleDefinitionRepo = new CosmosDbRepository<RoleDefinition>(
            controlPlaneClient, ContainerNames.RoleDefinitions);
        _denormalizationEngine = denormalizationEngine;
        _replayDetector = replayDetector;
        _logger = logger;
    }

    /// <summary>
    /// Processes a role assignment change.
    /// </summary>
    public async Task ProcessAsync(RoleAssignment assignment)
    {
        _logger.LogInformation(
            "Processing role assignment {AssignmentId} with changeId {ChangeId}",
            assignment.Id,
            assignment.Version?.ChangeId);

        // Check if this is a delete
        if (assignment.Lifecycle?.IsDeleted == true)
        {
            await HandleDeleteAsync(assignment);
            return;
        }

        // Get the role definition
        var roleDefinition = await _roleDefinitionRepo.GetByIdAsync(
            assignment.RoleDefinitionId,
            "RoleDefinition");

        if (roleDefinition == null)
        {
            _logger.LogWarning(
                "Role definition {RoleDefinitionId} not found for assignment {AssignmentId}",
                assignment.RoleDefinitionId,
                assignment.Id);
            return;
        }

        // Get or create EffectiveAccess document
        var effectiveAccessId = Rbac.Shared.Utilities.ScopeHasher.GenerateEffectiveAccessId(
            assignment.PrincipalId,
            assignment.Scope);

        var existing = await _effectiveAccessRepo.GetByIdAsync(
            effectiveAccessId,
            assignment.PrincipalId);

        if (existing != null)
        {
            // Check for replay
            var existingRole = existing.EffectiveRoles.FirstOrDefault(
                r => r.AssignmentId == assignment.Id);

            if (existingRole != null &&
                !_replayDetector.ShouldProcess(
                    assignment.Version?.ChangeId ?? "",
                    assignment.Version?.SequenceNumber ?? 0,
                    existingRole.SourceVersions))
            {
                _logger.LogDebug(
                    "Skipping replay of assignment {AssignmentId}",
                    assignment.Id);
                return;
            }

            // Update existing
            var updated = _denormalizationEngine.UpdateEffectiveAccess(
                existing,
                assignment,
                roleDefinition);

            await _effectiveAccessRepo.UpsertAsync(updated, updated.PrincipalId);
        }
        else
        {
            // Create new
            var effectiveAccess = _denormalizationEngine.CreateEffectiveAccess(
                assignment,
                roleDefinition);

            await _effectiveAccessRepo.CreateAsync(effectiveAccess, effectiveAccess.PrincipalId);
        }

        _logger.LogInformation(
            "Successfully processed role assignment {AssignmentId}",
            assignment.Id);
    }

    private async Task HandleDeleteAsync(RoleAssignment assignment)
    {
        var effectiveAccessId = Rbac.Shared.Utilities.ScopeHasher.GenerateEffectiveAccessId(
            assignment.PrincipalId,
            assignment.Scope);

        var existing = await _effectiveAccessRepo.GetByIdAsync(
            effectiveAccessId,
            assignment.PrincipalId);

        if (existing == null)
        {
            _logger.LogDebug(
                "EffectiveAccess not found for deleted assignment {AssignmentId}",
                assignment.Id);
            return;
        }

        var updated = _denormalizationEngine.RemoveRoleFromEffectiveAccess(
            existing,
            assignment.Id);

        if (updated == null)
        {
            // No more roles, delete the document
            await _effectiveAccessRepo.DeleteAsync(effectiveAccessId, assignment.PrincipalId);
            _logger.LogInformation(
                "Deleted EffectiveAccess document for principal {PrincipalId} at scope {Scope}",
                assignment.PrincipalId,
                assignment.Scope);
        }
        else
        {
            await _effectiveAccessRepo.UpsertAsync(updated, updated.PrincipalId);
        }
    }
}
