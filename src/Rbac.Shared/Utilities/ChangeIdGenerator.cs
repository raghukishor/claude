namespace Rbac.Shared.Utilities;

/// <summary>
/// Generates unique change IDs for idempotency tracking.
/// </summary>
public static class ChangeIdGenerator
{
    /// <summary>
    /// Generates a new change ID.
    /// Format: chg-{timestamp}-{random}
    /// </summary>
    public static string Generate()
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"chg-{timestamp}-{random}";
    }

    /// <summary>
    /// Extracts the timestamp from a change ID.
    /// </summary>
    public static DateTimeOffset? ExtractTimestamp(string changeId)
    {
        if (string.IsNullOrWhiteSpace(changeId))
            return null;

        var parts = changeId.Split('-');
        if (parts.Length < 2)
            return null;

        if (DateTimeOffset.TryParseExact(
            parts[1],
            "yyyyMMddHHmmss",
            null,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var timestamp))
        {
            return timestamp;
        }

        return null;
    }

    /// <summary>
    /// Validates a change ID format.
    /// </summary>
    public static bool IsValid(string changeId)
    {
        if (string.IsNullOrWhiteSpace(changeId))
            return false;

        var parts = changeId.Split('-');
        if (parts.Length != 3)
            return false;

        if (parts[0] != "chg")
            return false;

        if (parts[1].Length != 14)
            return false;

        if (parts[2].Length != 8)
            return false;

        return true;
    }
}
