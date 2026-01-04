using FluentAssertions;
using Rbac.DataPlane.Services;

namespace Rbac.DataPlane.Tests.Services;

public class ConditionEvaluatorTests
{
    private readonly ConditionEvaluator _evaluator;

    public ConditionEvaluatorTests()
    {
        _evaluator = new ConditionEvaluator();
    }

    [Fact]
    public void Evaluate_WithNullCondition_ShouldSatisfy()
    {
        var result = _evaluator.Evaluate(null, null);

        result.IsSatisfied.Should().BeTrue();
        result.Reason.Should().Contain("No condition specified");
    }

    [Fact]
    public void Evaluate_WithEmptyCondition_ShouldSatisfy()
    {
        var result = _evaluator.Evaluate("", null);

        result.IsSatisfied.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_StringEquals_Matching_ShouldSatisfy()
    {
        var condition = "@Resource[Microsoft.Storage/storageAccounts:name] StringEquals 'prod'";
        var attributes = new Dictionary<string, string>
        {
            { "Microsoft.Storage/storageAccounts:name", "prod" }
        };

        var result = _evaluator.Evaluate(condition, attributes);

        result.IsSatisfied.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_StringEquals_CaseInsensitive_ShouldSatisfy()
    {
        var condition = "@Resource[Microsoft.Storage/storageAccounts:name] StringEquals 'PROD'";
        var attributes = new Dictionary<string, string>
        {
            { "Microsoft.Storage/storageAccounts:name", "prod" }
        };

        var result = _evaluator.Evaluate(condition, attributes);

        result.IsSatisfied.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_StringEquals_NotMatching_ShouldNotSatisfy()
    {
        var condition = "@Resource[Microsoft.Storage/storageAccounts:name] StringEquals 'prod'";
        var attributes = new Dictionary<string, string>
        {
            { "Microsoft.Storage/storageAccounts:name", "dev" }
        };

        var result = _evaluator.Evaluate(condition, attributes);

        result.IsSatisfied.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_StringNotEquals_Matching_ShouldSatisfy()
    {
        var condition = "@Resource[Microsoft.Storage/storageAccounts:name] StringNotEquals 'prod'";
        var attributes = new Dictionary<string, string>
        {
            { "Microsoft.Storage/storageAccounts:name", "dev" }
        };

        var result = _evaluator.Evaluate(condition, attributes);

        result.IsSatisfied.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_StringNotEquals_NotMatching_ShouldNotSatisfy()
    {
        var condition = "@Resource[Microsoft.Storage/storageAccounts:name] StringNotEquals 'prod'";
        var attributes = new Dictionary<string, string>
        {
            { "Microsoft.Storage/storageAccounts:name", "prod" }
        };

        var result = _evaluator.Evaluate(condition, attributes);

        result.IsSatisfied.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_StringStartsWith_Matching_ShouldSatisfy()
    {
        var condition = "@Resource[Microsoft.Storage/storageAccounts:name] StringStartsWith 'prod-'";
        var attributes = new Dictionary<string, string>
        {
            { "Microsoft.Storage/storageAccounts:name", "prod-storage-001" }
        };

        var result = _evaluator.Evaluate(condition, attributes);

        result.IsSatisfied.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_StringStartsWith_NotMatching_ShouldNotSatisfy()
    {
        var condition = "@Resource[Microsoft.Storage/storageAccounts:name] StringStartsWith 'prod-'";
        var attributes = new Dictionary<string, string>
        {
            { "Microsoft.Storage/storageAccounts:name", "dev-storage-001" }
        };

        var result = _evaluator.Evaluate(condition, attributes);

        result.IsSatisfied.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_MissingAttribute_ShouldNotSatisfy()
    {
        var condition = "@Resource[Microsoft.Storage/storageAccounts:name] StringEquals 'prod'";
        var attributes = new Dictionary<string, string>();

        var result = _evaluator.Evaluate(condition, attributes);

        result.IsSatisfied.Should().BeFalse();
        result.Reason.Should().Contain("not found");
    }

    [Fact]
    public void Evaluate_InvalidFormat_ShouldNotSatisfy()
    {
        var condition = "invalid condition format";
        var attributes = new Dictionary<string, string>();

        var result = _evaluator.Evaluate(condition, attributes);

        result.IsSatisfied.Should().BeFalse();
        result.Reason.Should().Contain("Invalid condition expression");
    }
}
