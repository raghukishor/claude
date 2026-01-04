using FluentAssertions;
using Rbac.DataPlane.Services;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.DataPlane;

namespace Rbac.DataPlane.Tests.Services;

public class PermissionEvaluatorTests
{
    private readonly PermissionEvaluator _evaluator;

    public PermissionEvaluatorTests()
    {
        _evaluator = new PermissionEvaluator();
    }

    [Fact]
    public void Evaluate_WithExactMatch_ShouldAllow()
    {
        var role = CreateRole(actions: new[] { "Microsoft.Storage/storageAccounts/read" });

        var result = _evaluator.Evaluate("Microsoft.Storage/storageAccounts/read", role);

        result.IsAllowed.Should().BeTrue();
        result.MatchedPattern.Should().Be("Microsoft.Storage/storageAccounts/read");
    }

    [Fact]
    public void Evaluate_WithWildcardMatch_ShouldAllow()
    {
        var role = CreateRole(actions: new[] { "Microsoft.Storage/*" });

        var result = _evaluator.Evaluate("Microsoft.Storage/storageAccounts/read", role);

        result.IsAllowed.Should().BeTrue();
        result.MatchedPattern.Should().Be("Microsoft.Storage/*");
    }

    [Fact]
    public void Evaluate_WithUniversalWildcard_ShouldAllow()
    {
        var role = CreateRole(actions: new[] { "*" });

        var result = _evaluator.Evaluate("Microsoft.Storage/storageAccounts/read", role);

        result.IsAllowed.Should().BeTrue();
        result.MatchedPattern.Should().Be("*");
    }

    [Fact]
    public void Evaluate_WithNoMatch_ShouldDeny()
    {
        var role = CreateRole(actions: new[] { "Microsoft.Compute/*" });

        var result = _evaluator.Evaluate("Microsoft.Storage/storageAccounts/read", role);

        result.IsAllowed.Should().BeFalse();
        result.MatchedPattern.Should().BeNull();
    }

    [Fact]
    public void Evaluate_WithNotActions_ShouldDeny()
    {
        var role = CreateRole(
            actions: new[] { "Microsoft.Storage/*" },
            notActions: new[] { "Microsoft.Storage/storageAccounts/delete" });

        var result = _evaluator.Evaluate("Microsoft.Storage/storageAccounts/delete", role);

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("notActions");
    }

    [Fact]
    public void Evaluate_WithNotActionsNotMatching_ShouldAllow()
    {
        var role = CreateRole(
            actions: new[] { "Microsoft.Storage/*" },
            notActions: new[] { "Microsoft.Storage/storageAccounts/delete" });

        var result = _evaluator.Evaluate("Microsoft.Storage/storageAccounts/read", role);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void EvaluateDataAction_WithExactMatch_ShouldAllow()
    {
        var role = CreateRole(dataActions: new[] { "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read" });

        var result = _evaluator.EvaluateDataAction("Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read", role);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void EvaluateDataAction_WithWildcard_ShouldAllow()
    {
        var role = CreateRole(dataActions: new[] { "Microsoft.Storage/storageAccounts/blobServices/*" });

        var result = _evaluator.EvaluateDataAction("Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read", role);

        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void EvaluateDataAction_WithNotDataActions_ShouldDeny()
    {
        var role = CreateRole(
            dataActions: new[] { "Microsoft.Storage/storageAccounts/blobServices/*" },
            notDataActions: new[] { "Microsoft.Storage/storageAccounts/blobServices/containers/blobs/delete" });

        var result = _evaluator.EvaluateDataAction("Microsoft.Storage/storageAccounts/blobServices/containers/blobs/delete", role);

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("notDataActions");
    }

    [Fact]
    public void EvaluateDataAction_WithNoMatch_ShouldDeny()
    {
        var role = CreateRole(dataActions: new[] { "Microsoft.Compute/*" });

        var result = _evaluator.EvaluateDataAction("Microsoft.Storage/storageAccounts/blobServices/containers/blobs/read", role);

        result.IsAllowed.Should().BeFalse();
    }

    private static EffectiveRole CreateRole(
        string[]? actions = null,
        string[]? notActions = null,
        string[]? dataActions = null,
        string[]? notDataActions = null)
    {
        return new EffectiveRole
        {
            RoleDefinitionId = "rd-test",
            RoleName = "Test Role",
            AssignmentId = "ra-test",
            AssignmentScope = "/subscriptions/sub-001",
            Permissions = new Permission
            {
                Actions = actions?.ToList() ?? new List<string>(),
                NotActions = notActions?.ToList() ?? new List<string>(),
                DataActions = dataActions?.ToList() ?? new List<string>(),
                NotDataActions = notDataActions?.ToList() ?? new List<string>()
            }
        };
    }
}
