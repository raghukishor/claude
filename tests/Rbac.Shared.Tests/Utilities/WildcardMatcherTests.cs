using FluentAssertions;
using Rbac.Shared.Utilities;

namespace Rbac.Shared.Tests.Utilities;

public class WildcardMatcherTests
{
    [Theory]
    [InlineData("Microsoft.Storage/storageAccounts/read", "Microsoft.Storage/storageAccounts/read", true)]
    [InlineData("Microsoft.Storage/storageAccounts/read", "microsoft.storage/storageaccounts/read", true)]
    [InlineData("Microsoft.Storage/storageAccounts/read", "Microsoft.Storage/storageAccounts/write", false)]
    public void Matches_ExactMatch_ShouldWork(string action, string pattern, bool expected)
    {
        var result = WildcardMatcher.Matches(action, pattern);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Microsoft.Storage/storageAccounts/read", "*", true)]
    [InlineData("anything/at/all", "*", true)]
    public void Matches_UniversalWildcard_ShouldMatchEverything(string action, string pattern, bool expected)
    {
        var result = WildcardMatcher.Matches(action, pattern);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Microsoft.Storage/storageAccounts/read", "Microsoft.Storage/*", true)]
    [InlineData("Microsoft.Storage/storageAccounts/write", "Microsoft.Storage/*", true)]
    [InlineData("Microsoft.Compute/virtualMachines/read", "Microsoft.Storage/*", false)]
    public void Matches_SuffixWildcard_ShouldMatchPrefix(string action, string pattern, bool expected)
    {
        var result = WildcardMatcher.Matches(action, pattern);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Microsoft.Storage/storageAccounts/read", "*/read", true)]
    [InlineData("Microsoft.Compute/virtualMachines/read", "*/read", true)]
    [InlineData("Microsoft.Storage/storageAccounts/write", "*/read", false)]
    public void Matches_PrefixWildcard_ShouldMatchSuffix(string action, string pattern, bool expected)
    {
        var result = WildcardMatcher.Matches(action, pattern);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Microsoft.Storage/storageAccounts/blobServices/read", "Microsoft.Storage/*/read", true)]
    [InlineData("Microsoft.Storage/containers/read", "Microsoft.Storage/*/read", true)]
    [InlineData("Microsoft.Storage/storageAccounts/write", "Microsoft.Storage/*/read", false)]
    public void Matches_MiddleWildcard_ShouldMatchMiddleContent(string action, string pattern, bool expected)
    {
        var result = WildcardMatcher.Matches(action, pattern);

        result.Should().Be(expected);
    }

    [Fact]
    public void MatchesAny_WithMatchingPattern_ShouldReturnPattern()
    {
        var action = "Microsoft.Storage/storageAccounts/read";
        var patterns = new[]
        {
            "Microsoft.Compute/*",
            "Microsoft.Storage/*",
            "*/write"
        };

        var result = WildcardMatcher.MatchesAny(action, patterns);

        result.Should().Be("Microsoft.Storage/*");
    }

    [Fact]
    public void MatchesAny_WithNoMatchingPattern_ShouldReturnNull()
    {
        var action = "Microsoft.Storage/storageAccounts/read";
        var patterns = new[]
        {
            "Microsoft.Compute/*",
            "*/write"
        };

        var result = WildcardMatcher.MatchesAny(action, patterns);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("Microsoft.Storage/*", true)]
    [InlineData("*/read", true)]
    [InlineData("*", true)]
    [InlineData("Microsoft.Storage/storageAccounts/read", true)]
    [InlineData("", false)]
    [InlineData("Microsoft.Storage/storage Accounts/read", false)]
    public void IsValidPattern_ShouldValidateCorrectly(string pattern, bool expected)
    {
        var result = WildcardMatcher.IsValidPattern(pattern);

        result.Should().Be(expected);
    }

    [Fact]
    public void Matches_WithEmptyAction_ShouldReturnFalse()
    {
        var result = WildcardMatcher.Matches("", "Microsoft.Storage/*");

        result.Should().BeFalse();
    }

    [Fact]
    public void Matches_WithEmptyPattern_ShouldReturnFalse()
    {
        var result = WildcardMatcher.Matches("Microsoft.Storage/read", "");

        result.Should().BeFalse();
    }
}
