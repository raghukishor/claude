using FluentAssertions;
using Rbac.Shared.Utilities;

namespace Rbac.Shared.Tests.Utilities;

public class ScopeParserTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("/subscriptions/sub-001", true)]
    [InlineData("/subscriptions/sub-001/resourceGroups/rg-prod", true)]
    [InlineData("", false)]
    [InlineData("subscriptions/sub-001", false)]
    [InlineData("/subscriptions//sub-001", false)]
    public void IsValidScope_ShouldValidateCorrectly(string scope, bool expected)
    {
        var result = ScopeParser.IsValidScope(scope);
        result.Should().Be(expected);
    }

    [Fact]
    public void GetScopeHierarchy_WithRootScope_ShouldReturnSingleElement()
    {
        var result = ScopeParser.GetScopeHierarchy("/");

        result.Should().HaveCount(1);
        result[0].Should().Be("/");
    }

    [Fact]
    public void GetScopeHierarchy_WithNestedScope_ShouldReturnFullHierarchy()
    {
        var scope = "/subscriptions/sub-001/resourceGroups/rg-prod";

        var result = ScopeParser.GetScopeHierarchy(scope);

        result.Should().HaveCount(3);
        result[0].Should().Be("/subscriptions/sub-001/resourceGroups/rg-prod");
        result[1].Should().Be("/subscriptions/sub-001");
        result[2].Should().Be("/");
    }

    [Fact]
    public void GetScopeHierarchy_WithDeepScope_ShouldReturnAllLevels()
    {
        var scope = "/tenants/tenant-1/subscriptions/sub-001/resourceGroups/rg-prod";

        var result = ScopeParser.GetScopeHierarchy(scope);

        result.Should().Contain("/tenants/tenant-1/subscriptions/sub-001/resourceGroups/rg-prod");
        result.Should().Contain("/tenants/tenant-1/subscriptions/sub-001");
        result.Should().Contain("/tenants/tenant-1");
        result.Should().Contain("/");
    }

    [Fact]
    public void GetParentScope_WithNestedScope_ShouldReturnParent()
    {
        var scope = "/subscriptions/sub-001/resourceGroups/rg-prod";

        var result = ScopeParser.GetParentScope(scope);

        result.Should().Be("/subscriptions/sub-001");
    }

    [Fact]
    public void GetParentScope_WithRootScope_ShouldReturnNull()
    {
        var result = ScopeParser.GetParentScope("/");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("/", 0)]
    [InlineData("/subscriptions/sub-001", 1)]
    [InlineData("/subscriptions/sub-001/resourceGroups/rg-prod", 2)]
    public void GetScopeDepth_ShouldCalculateCorrectly(string scope, int expectedDepth)
    {
        var result = ScopeParser.GetScopeDepth(scope);

        result.Should().Be(expectedDepth);
    }

    [Theory]
    [InlineData("/", "/subscriptions/sub-001", true)]
    [InlineData("/subscriptions/sub-001", "/subscriptions/sub-001/resourceGroups/rg-prod", true)]
    [InlineData("/subscriptions/sub-001", "/subscriptions/sub-001", true)]
    [InlineData("/subscriptions/sub-001", "/subscriptions/sub-002", false)]
    [InlineData("/subscriptions/sub-001/resourceGroups/rg-prod", "/subscriptions/sub-001", false)]
    public void IsChildScope_ShouldDetermineCorrectly(string parent, string child, bool expected)
    {
        var result = ScopeParser.IsChildScope(parent, child);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("  /subscriptions/sub-001  ", "/subscriptions/sub-001")]
    [InlineData("/subscriptions/sub-001/", "/subscriptions/sub-001")]
    [InlineData("", "/")]
    [InlineData("   ", "/")]
    public void NormalizeScope_ShouldNormalizeCorrectly(string input, string expected)
    {
        var result = ScopeParser.NormalizeScope(input);

        result.Should().Be(expected);
    }
}
