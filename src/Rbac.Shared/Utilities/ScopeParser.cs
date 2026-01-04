using System.Text.RegularExpressions;

namespace Rbac.Shared.Utilities;

/// <summary>
/// Utility for parsing and working with scope strings.
/// </summary>
public static class ScopeParser
{
    private static readonly Regex ScopePattern = new(
        @"^(/|(/[a-zA-Z0-9-_]+)+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Validates a scope string.
    /// </summary>
    public static bool IsValidScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return false;

        return ScopePattern.IsMatch(scope);
    }

    /// <summary>
    /// Gets the hierarchy of scopes from a scope string.
    /// Returns scopes from most specific to root.
    /// </summary>
    /// <example>
    /// Input: /subscriptions/sub-001/resourceGroups/rg-prod
    /// Output: [
    ///   "/subscriptions/sub-001/resourceGroups/rg-prod",
    ///   "/subscriptions/sub-001",
    ///   "/"
    /// ]
    /// </example>
    public static List<string> GetScopeHierarchy(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return new List<string> { "/" };

        var hierarchy = new List<string> { scope };

        if (scope == "/")
            return hierarchy;

        var segments = scope.Split('/', StringSplitOptions.RemoveEmptyEntries);

        // Walk up the hierarchy, removing two segments at a time
        // (resource type + resource name pairs in Azure-style scopes)
        for (int numSegments = segments.Length - 2; numSegments >= 0; numSegments -= 2)
        {
            if (numSegments == 0)
            {
                hierarchy.Add("/");
            }
            else
            {
                hierarchy.Add("/" + string.Join("/", segments.Take(numSegments)));
            }
        }

        return hierarchy;
    }

    /// <summary>
    /// Gets the parent scope of a given scope.
    /// </summary>
    public static string? GetParentScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope == "/")
            return null;

        var segments = scope.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length <= 2)
            return "/";

        return "/" + string.Join("/", segments.Take(segments.Length - 2));
    }

    /// <summary>
    /// Calculates the depth of a scope (0 for root).
    /// </summary>
    public static int GetScopeDepth(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope == "/")
            return 0;

        var segments = scope.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length / 2;
    }

    /// <summary>
    /// Checks if childScope is under parentScope.
    /// </summary>
    public static bool IsChildScope(string parentScope, string childScope)
    {
        if (parentScope == "/")
            return true;

        if (string.IsNullOrWhiteSpace(childScope))
            return false;

        return childScope.StartsWith(parentScope + "/", StringComparison.OrdinalIgnoreCase)
            || childScope.Equals(parentScope, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes a scope string (lowercase, trim trailing slashes).
    /// </summary>
    public static string NormalizeScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return "/";

        scope = scope.Trim().TrimEnd('/');

        if (string.IsNullOrEmpty(scope))
            return "/";

        return scope;
    }
}
