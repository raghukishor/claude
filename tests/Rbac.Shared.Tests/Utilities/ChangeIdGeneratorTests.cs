using FluentAssertions;
using Rbac.Shared.Utilities;

namespace Rbac.Shared.Tests.Utilities;

public class ChangeIdGeneratorTests
{
    [Fact]
    public void Generate_ShouldReturnValidFormat()
    {
        var changeId = ChangeIdGenerator.Generate();

        changeId.Should().StartWith("chg-");
        changeId.Split('-').Should().HaveCount(3);
        ChangeIdGenerator.IsValid(changeId).Should().BeTrue();
    }

    [Fact]
    public void Generate_ShouldReturnUniqueIds()
    {
        var ids = Enumerable.Range(0, 100)
            .Select(_ => ChangeIdGenerator.Generate())
            .ToList();

        ids.Distinct().Should().HaveCount(100);
    }

    [Theory]
    [InlineData("chg-20250115103000-a1b2c3d4", true)]
    [InlineData("chg-20250115103000-12345678", true)]
    [InlineData("chg-20250115103000", false)]
    [InlineData("chg-2025011510-a1b2c3d4", false)]
    [InlineData("xyz-20250115103000-a1b2c3d4", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_ShouldValidateCorrectly(string? changeId, bool expected)
    {
        var result = ChangeIdGenerator.IsValid(changeId!);

        result.Should().Be(expected);
    }

    [Fact]
    public void ExtractTimestamp_WithValidChangeId_ShouldReturnTimestamp()
    {
        var changeId = "chg-20250115103000-a1b2c3d4";

        var result = ChangeIdGenerator.ExtractTimestamp(changeId);

        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(2025);
        result.Value.Month.Should().Be(1);
        result.Value.Day.Should().Be(15);
        result.Value.Hour.Should().Be(10);
        result.Value.Minute.Should().Be(30);
        result.Value.Second.Should().Be(0);
    }

    [Fact]
    public void ExtractTimestamp_WithInvalidChangeId_ShouldReturnNull()
    {
        var result = ChangeIdGenerator.ExtractTimestamp("invalid");

        result.Should().BeNull();
    }

    [Fact]
    public void Generate_ShouldContainCurrentTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var changeId = ChangeIdGenerator.Generate();
        var after = DateTimeOffset.UtcNow;

        var timestamp = ChangeIdGenerator.ExtractTimestamp(changeId);

        timestamp.Should().NotBeNull();
        timestamp!.Value.Should().BeOnOrAfter(before.AddSeconds(-1));
        timestamp.Value.Should().BeOnOrBefore(after.AddSeconds(1));
    }
}
