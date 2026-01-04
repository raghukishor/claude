using System.Security.Cryptography;
using System.Text;

namespace Rbac.Shared.Utilities;

/// <summary>
/// Generates deterministic hashes for scope strings.
/// </summary>
public static class ScopeHasher
{
    /// <summary>
    /// Computes a short hash for a scope string.
    /// Used in document IDs: ea-{principalId}-{scopeHash}
    /// </summary>
    public static string ComputeHash(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            scope = "/";

        var normalizedScope = ScopeParser.NormalizeScope(scope).ToLowerInvariant();

        var bytes = Encoding.UTF8.GetBytes(normalizedScope);
        var hash = SHA256.HashData(bytes);

        // Take first 8 bytes and convert to hex (16 chars)
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a document ID for an EffectiveAccess document.
    /// </summary>
    public static string GenerateEffectiveAccessId(string principalId, string scope)
    {
        var scopeHash = ComputeHash(scope);
        return $"ea-{principalId}-{scopeHash}";
    }

    /// <summary>
    /// Generates a role definition ID.
    /// </summary>
    public static string GenerateRoleDefinitionId()
    {
        return $"rd-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Generates a role assignment ID.
    /// </summary>
    public static string GenerateRoleAssignmentId()
    {
        return $"ra-{Guid.NewGuid():N}";
    }
}
