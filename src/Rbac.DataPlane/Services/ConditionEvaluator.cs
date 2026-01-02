using System.Text.RegularExpressions;

namespace Rbac.DataPlane.Services;

/// <summary>
/// Simplified condition evaluator for MVP.
/// Supports basic expressions like:
/// @Resource[Microsoft.Storage/storageAccounts:name] StringEquals 'prod'
/// </summary>
public class ConditionEvaluator
{
    private static readonly Regex ConditionPattern = new(
        @"@Resource\[([^\]]+)\]\s+(StringEquals|StringNotEquals|StringStartsWith)\s+'([^']+)'",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Evaluates a condition expression against resource attributes.
    /// </summary>
    public ConditionEvaluationResult Evaluate(
        string? conditionExpression,
        Dictionary<string, string>? resourceAttributes)
    {
        if (string.IsNullOrWhiteSpace(conditionExpression))
        {
            return new ConditionEvaluationResult
            {
                IsSatisfied = true,
                Reason = "No condition specified"
            };
        }

        resourceAttributes ??= new Dictionary<string, string>();

        var match = ConditionPattern.Match(conditionExpression);
        if (!match.Success)
        {
            return new ConditionEvaluationResult
            {
                IsSatisfied = false,
                Reason = $"Invalid condition expression format: {conditionExpression}"
            };
        }

        var attributePath = match.Groups[1].Value;
        var operatorName = match.Groups[2].Value;
        var expectedValue = match.Groups[3].Value;

        // Get the actual value from resource attributes
        if (!resourceAttributes.TryGetValue(attributePath, out var actualValue))
        {
            return new ConditionEvaluationResult
            {
                IsSatisfied = false,
                Reason = $"Resource attribute not found: {attributePath}"
            };
        }

        var result = EvaluateOperator(operatorName, actualValue, expectedValue);
        return new ConditionEvaluationResult
        {
            IsSatisfied = result,
            Reason = result
                ? $"Condition satisfied: {attributePath} {operatorName} '{expectedValue}'"
                : $"Condition not satisfied: {actualValue} does not match {operatorName} '{expectedValue}'"
        };
    }

    private static bool EvaluateOperator(string operatorName, string actualValue, string expectedValue)
    {
        return operatorName.ToLowerInvariant() switch
        {
            "stringequals" => string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            "stringnotequals" => !string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase),
            "stringstartswith" => actualValue.StartsWith(expectedValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}

public class ConditionEvaluationResult
{
    public bool IsSatisfied { get; set; }
    public string Reason { get; set; } = string.Empty;
}
