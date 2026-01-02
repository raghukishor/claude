using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.DataPlane;

namespace Rbac.SyncService.Idempotency;

/// <summary>
/// Detects replay of change events to ensure idempotent processing.
/// </summary>
public class ReplayDetector
{
    /// <summary>
    /// Determines if a role assignment change should be processed based on version tracking.
    /// </summary>
    public bool ShouldProcess(
        string changeId,
        long sequenceNumber,
        SourceVersions? existingVersions)
    {
        if (existingVersions == null)
            return true;

        // If we've already processed this change ID, skip
        if (existingVersions.AssignmentChangeId == changeId)
            return false;

        // If the new sequence is not greater, skip (out of order)
        if (sequenceNumber <= existingVersions.AssignmentSequence)
            return false;

        return true;
    }

    /// <summary>
    /// Determines if a role definition change should trigger updates to EffectiveAccess docs.
    /// </summary>
    public bool ShouldProcessDefinitionChange(
        string changeId,
        long sequenceNumber,
        SourceVersions? existingVersions)
    {
        if (existingVersions == null)
            return true;

        // If we've already processed this change ID, skip
        if (existingVersions.RoleDefChangeId == changeId)
            return false;

        // If the new sequence is not greater, skip
        if (sequenceNumber <= existingVersions.RoleDefSequence)
            return false;

        return true;
    }

    /// <summary>
    /// Compares version vectors to determine processing priority.
    /// </summary>
    public bool IsNewerVersion(VersionInfo newVersion, VersionInfo? existingVersion)
    {
        if (existingVersion == null)
            return true;

        // Compare by sequence number first
        if (newVersion.SequenceNumber > existingVersion.SequenceNumber)
            return true;

        if (newVersion.SequenceNumber < existingVersion.SequenceNumber)
            return false;

        // If sequence numbers are equal, compare timestamps
        return newVersion.Timestamp > existingVersion.Timestamp;
    }
}
