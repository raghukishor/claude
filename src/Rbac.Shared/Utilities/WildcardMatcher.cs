namespace Rbac.Shared.Utilities;

/// <summary>
/// Utility for matching action strings against permission patterns.
/// </summary>
public static class WildcardMatcher
{
    /// <summary>
    /// Checks if an action matches any of the given patterns.
    /// </summary>
    /// <param name="action">The action to check (e.g., "Microsoft.Storage/storageAccounts/read")</param>
    /// <param name="patterns">The patterns to match against (e.g., "Microsoft.Storage/*")</param>
    /// <returns>The matching pattern, or null if no match</returns>
    public static string? MatchesAny(string action, IEnumerable<string> patterns)
    {
        if (string.IsNullOrWhiteSpace(action))
            return null;

        foreach (var pattern in patterns)
        {
            if (Matches(action, pattern))
                return pattern;
        }

        return null;
    }

    /// <summary>
    /// Checks if an action matches a permission pattern.
    /// </summary>
    /// <param name="action">The action to check</param>
    /// <param name="pattern">The pattern to match against</param>
    /// <returns>True if the action matches the pattern</returns>
    public static bool Matches(string action, string pattern)
    {
        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(pattern))
            return false;

        // Exact match
        if (pattern.Equals(action, StringComparison.OrdinalIgnoreCase))
            return true;

        // Universal wildcard
        if (pattern == "*")
            return true;

        // Handle patterns with wildcards
        if (pattern.Contains('*'))
        {
            return MatchWithWildcard(action, pattern);
        }

        return false;
    }

    private static bool MatchWithWildcard(string action, string pattern)
    {
        // Split pattern into parts
        var patternParts = pattern.Split('*');

        // Simple patterns like "Microsoft.Storage/*" or "*/read"
        if (patternParts.Length == 2)
        {
            var prefix = patternParts[0];
            var suffix = patternParts[1];

            // Pattern ends with * (prefix match)
            if (string.IsNullOrEmpty(suffix))
            {
                return action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            // Pattern starts with * (suffix match)
            if (string.IsNullOrEmpty(prefix))
            {
                return action.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
            }

            // Pattern has * in the middle
            return action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && action.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                && action.Length >= prefix.Length + suffix.Length;
        }

        // Multiple wildcards - use regex-like matching
        return MatchMultipleWildcards(action, patternParts);
    }

    private static bool MatchMultipleWildcards(string action, string[] patternParts)
    {
        var currentIndex = 0;

        for (int i = 0; i < patternParts.Length; i++)
        {
            var part = patternParts[i];

            if (string.IsNullOrEmpty(part))
                continue;

            if (i == 0)
            {
                // First part must be a prefix
                if (!action.StartsWith(part, StringComparison.OrdinalIgnoreCase))
                    return false;
                currentIndex = part.Length;
            }
            else if (i == patternParts.Length - 1)
            {
                // Last part must be a suffix
                if (!action.EndsWith(part, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else
            {
                // Middle parts must exist somewhere
                var foundIndex = action.IndexOf(part, currentIndex, StringComparison.OrdinalIgnoreCase);
                if (foundIndex < 0)
                    return false;
                currentIndex = foundIndex + part.Length;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates a permission pattern.
    /// </summary>
    public static bool IsValidPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        // Basic validation: pattern should contain alphanumeric, dots, slashes, and wildcards
        return pattern.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '/' || c == '*' || c == '-' || c == '_');
    }
}
