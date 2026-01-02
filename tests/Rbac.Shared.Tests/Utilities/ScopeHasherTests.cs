using FluentAssertions;
using Rbac.Shared.Utilities;

namespace Rbac.Shared.Tests.Utilities;

public class ScopeHasherTests
{
    [Fact]
    public void ComputeHash_ShouldReturnConsistentHash()
    {
        var scope = "/subscriptions/sub-001/resourceGroups/rg-prod";

        var hash1 = ScopeHasher.ComputeHash(scope);
        var hash2 = ScopeHasher.ComputeHash(scope);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_ShouldReturnDifferentHashesForDifferentScopes()
    {
        var hash1 = ScopeHasher.ComputeHash("/subscriptions/sub-001");
        var hash2 = ScopeHasher.ComputeHash("/subscriptions/sub-002");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_ShouldBeCaseInsensitive()
    {
        var hash1 = ScopeHasher.ComputeHash("/subscriptions/SUB-001");
        var hash2 = ScopeHasher.ComputeHash("/subscriptions/sub-001");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_ShouldReturn16CharHex()
    {
        var hash = ScopeHasher.ComputeHash("/subscriptions/sub-001");

        hash.Should().HaveLength(16);
        hash.Should().MatchRegex("^[0-9a-f]+$");
    }

    [Fact]
    public void GenerateEffectiveAccessId_ShouldIncludePrincipalAndScopeHash()
    {
        var principalId = "user-123";
        var scope = "/subscriptions/sub-001";

        var id = ScopeHasher.GenerateEffectiveAccessId(principalId, scope);

        id.Should().StartWith("ea-user-123-");
        id.Should().MatchRegex(@"^ea-user-123-[0-9a-f]{16}$");
    }

    [Fact]
    public void GenerateEffectiveAccessId_ShouldBeConsistent()
    {
        var principalId = "user-123";
        var scope = "/subscriptions/sub-001";

        var id1 = ScopeHasher.GenerateEffectiveAccessId(principalId, scope);
        var id2 = ScopeHasher.GenerateEffectiveAccessId(principalId, scope);

        id1.Should().Be(id2);
    }

    [Fact]
    public void GenerateRoleDefinitionId_ShouldStartWithRd()
    {
        var id = ScopeHasher.GenerateRoleDefinitionId();

        id.Should().StartWith("rd-");
        id.Should().HaveLength(35); // rd- + 32 hex chars
    }

    [Fact]
    public void GenerateRoleAssignmentId_ShouldStartWithRa()
    {
        var id = ScopeHasher.GenerateRoleAssignmentId();

        id.Should().StartWith("ra-");
        id.Should().HaveLength(35); // ra- + 32 hex chars
    }

    [Fact]
    public void GenerateRoleDefinitionId_ShouldBeUnique()
    {
        var ids = Enumerable.Range(0, 100)
            .Select(_ => ScopeHasher.GenerateRoleDefinitionId())
            .ToList();

        ids.Distinct().Should().HaveCount(100);
    }
}
