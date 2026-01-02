using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.ControlPlane;
using Rbac.Shared.Models.DataPlane;
using Rbac.Shared.Utilities;

namespace Rbac.SyncService.Processors;

/// <summary>
/// Handles denormalization of role assignments into EffectiveAccess documents.
/// </summary>
public class DenormalizationEngine
{
    /// <summary>
    /// Creates an EffectiveAccess document from a role assignment and role definition.
    /// </summary>
    public EffectiveAccess CreateEffectiveAccess(
        RoleAssignment assignment,
        RoleDefinition definition)
    {
        var effectiveRole = CreateEffectiveRole(assignment, definition);
        var scopeHash = ScopeHasher.ComputeHash(assignment.Scope);

        return new EffectiveAccess
        {
            Id = ScopeHasher.GenerateEffectiveAccessId(assignment.PrincipalId, assignment.Scope),
            PrincipalId = assignment.PrincipalId,
            Scope = assignment.Scope,
            ScopeHash = scopeHash,
            ScopeDepth = ScopeParser.GetScopeDepth(assignment.Scope),
            EffectiveRoles = new List<EffectiveRole> { effectiveRole },
            Sync = new SyncMetadata
            {
                DocumentVersion = 1,
                LastSyncedAt = DateTimeOffset.UtcNow,
                LastProcessedChangeIds = new List<string> { assignment.Version?.ChangeId ?? "" },
                SyncMode = SyncModes.Incremental
            },
            Watermarks = new Watermarks
            {
                HighWatermark = DateTimeOffset.UtcNow,
                AssignmentHighWatermark = assignment.Version?.Timestamp ?? DateTimeOffset.UtcNow
            }
        };
    }

    /// <summary>
    /// Creates an EffectiveRole from a role assignment and definition.
    /// </summary>
    public EffectiveRole CreateEffectiveRole(
        RoleAssignment assignment,
        RoleDefinition definition)
    {
        // Merge all permissions from the role definition
        var mergedPermission = MergePermissions(definition.Permissions);

        return new EffectiveRole
        {
            RoleDefinitionId = definition.Id,
            RoleName = definition.Name,
            AssignmentId = assignment.Id,
            AssignmentScope = assignment.Scope,
            Inherited = false,
            Condition = assignment.Condition,
            Permissions = mergedPermission,
            SourceVersions = new SourceVersions
            {
                AssignmentSequence = assignment.Version?.SequenceNumber ?? 0,
                AssignmentChangeId = assignment.Version?.ChangeId ?? "",
                RoleDefSequence = definition.Version?.SequenceNumber ?? 0,
                RoleDefChangeId = definition.Version?.ChangeId ?? ""
            }
        };
    }

    /// <summary>
    /// Updates an existing EffectiveAccess document by adding or updating a role.
    /// </summary>
    public EffectiveAccess UpdateEffectiveAccess(
        EffectiveAccess existing,
        RoleAssignment assignment,
        RoleDefinition definition)
    {
        var newRole = CreateEffectiveRole(assignment, definition);

        // Remove existing role for this assignment if present
        existing.EffectiveRoles.RemoveAll(r => r.AssignmentId == assignment.Id);

        // Add the new/updated role
        existing.EffectiveRoles.Add(newRole);

        // Update sync metadata
        existing.Sync.DocumentVersion++;
        existing.Sync.LastSyncedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrEmpty(assignment.Version?.ChangeId))
        {
            existing.Sync.LastProcessedChangeIds.Add(assignment.Version.ChangeId);
            // Keep only last 10 change IDs
            if (existing.Sync.LastProcessedChangeIds.Count > 10)
            {
                existing.Sync.LastProcessedChangeIds = existing.Sync.LastProcessedChangeIds
                    .TakeLast(10)
                    .ToList();
            }
        }

        // Update watermarks
        existing.Watermarks.HighWatermark = DateTimeOffset.UtcNow;
        existing.Watermarks.AssignmentHighWatermark = assignment.Version?.Timestamp ?? DateTimeOffset.UtcNow;

        return existing;
    }

    /// <summary>
    /// Removes a role from an EffectiveAccess document.
    /// </summary>
    public EffectiveAccess? RemoveRoleFromEffectiveAccess(
        EffectiveAccess existing,
        string assignmentId)
    {
        existing.EffectiveRoles.RemoveAll(r => r.AssignmentId == assignmentId);

        if (existing.EffectiveRoles.Count == 0)
        {
            // No more roles, document should be deleted
            return null;
        }

        // Update sync metadata
        existing.Sync.DocumentVersion++;
        existing.Sync.LastSyncedAt = DateTimeOffset.UtcNow;

        return existing;
    }

    /// <summary>
    /// Updates role definition info in all affected EffectiveRoles.
    /// </summary>
    public EffectiveAccess UpdateRoleDefinition(
        EffectiveAccess existing,
        RoleDefinition definition)
    {
        var mergedPermission = MergePermissions(definition.Permissions);

        foreach (var role in existing.EffectiveRoles.Where(r => r.RoleDefinitionId == definition.Id))
        {
            role.RoleName = definition.Name;
            role.Permissions = mergedPermission;
            role.SourceVersions.RoleDefSequence = definition.Version?.SequenceNumber ?? 0;
            role.SourceVersions.RoleDefChangeId = definition.Version?.ChangeId ?? "";
        }

        existing.Sync.DocumentVersion++;
        existing.Sync.LastSyncedAt = DateTimeOffset.UtcNow;
        existing.Watermarks.RoleDefHighWatermark = definition.Version?.Timestamp ?? DateTimeOffset.UtcNow;

        return existing;
    }

    private Permission MergePermissions(List<Permission> permissions)
    {
        var merged = new Permission
        {
            Actions = new List<string>(),
            NotActions = new List<string>(),
            DataActions = new List<string>(),
            NotDataActions = new List<string>()
        };

        foreach (var perm in permissions)
        {
            merged.Actions.AddRange(perm.Actions);
            merged.NotActions.AddRange(perm.NotActions);
            merged.DataActions.AddRange(perm.DataActions);
            merged.NotDataActions.AddRange(perm.NotDataActions);
        }

        // Remove duplicates
        merged.Actions = merged.Actions.Distinct().ToList();
        merged.NotActions = merged.NotActions.Distinct().ToList();
        merged.DataActions = merged.DataActions.Distinct().ToList();
        merged.NotDataActions = merged.NotDataActions.Distinct().ToList();

        return merged;
    }
}
