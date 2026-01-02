using FluentAssertions;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.DataPlane;
using Rbac.SyncService.Idempotency;

namespace Rbac.SyncService.Tests.Idempotency;

public class ReplayDetectorTests
{
    private readonly ReplayDetector _detector;

    public ReplayDetectorTests()
    {
        _detector = new ReplayDetector();
    }

    [Fact]
    public void ShouldProcess_WithNullExistingVersions_ShouldReturnTrue()
    {
        var result = _detector.ShouldProcess("chg-001", 1, null);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldProcess_WithSameChangeId_ShouldReturnFalse()
    {
        var existing = new SourceVersions
        {
            AssignmentChangeId = "chg-001",
            AssignmentSequence = 1
        };

        var result = _detector.ShouldProcess("chg-001", 2, existing);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldProcess_WithLowerSequence_ShouldReturnFalse()
    {
        var existing = new SourceVersions
        {
            AssignmentChangeId = "chg-001",
            AssignmentSequence = 5
        };

        var result = _detector.ShouldProcess("chg-002", 3, existing);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldProcess_WithHigherSequence_ShouldReturnTrue()
    {
        var existing = new SourceVersions
        {
            AssignmentChangeId = "chg-001",
            AssignmentSequence = 3
        };

        var result = _detector.ShouldProcess("chg-002", 5, existing);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldProcessDefinitionChange_WithNullExistingVersions_ShouldReturnTrue()
    {
        var result = _detector.ShouldProcessDefinitionChange("chg-001", 1, null);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldProcessDefinitionChange_WithSameChangeId_ShouldReturnFalse()
    {
        var existing = new SourceVersions
        {
            RoleDefChangeId = "chg-001",
            RoleDefSequence = 1
        };

        var result = _detector.ShouldProcessDefinitionChange("chg-001", 2, existing);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldProcessDefinitionChange_WithHigherSequence_ShouldReturnTrue()
    {
        var existing = new SourceVersions
        {
            RoleDefChangeId = "chg-001",
            RoleDefSequence = 3
        };

        var result = _detector.ShouldProcessDefinitionChange("chg-002", 5, existing);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsNewerVersion_WithNullExisting_ShouldReturnTrue()
    {
        var newVersion = new VersionInfo { SequenceNumber = 1 };

        var result = _detector.IsNewerVersion(newVersion, null);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsNewerVersion_WithHigherSequence_ShouldReturnTrue()
    {
        var newVersion = new VersionInfo { SequenceNumber = 5, Timestamp = DateTimeOffset.UtcNow };
        var existingVersion = new VersionInfo { SequenceNumber = 3, Timestamp = DateTimeOffset.UtcNow };

        var result = _detector.IsNewerVersion(newVersion, existingVersion);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsNewerVersion_WithLowerSequence_ShouldReturnFalse()
    {
        var newVersion = new VersionInfo { SequenceNumber = 2, Timestamp = DateTimeOffset.UtcNow };
        var existingVersion = new VersionInfo { SequenceNumber = 5, Timestamp = DateTimeOffset.UtcNow };

        var result = _detector.IsNewerVersion(newVersion, existingVersion);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsNewerVersion_WithSameSequenceNewerTimestamp_ShouldReturnTrue()
    {
        var existingTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var newTime = DateTimeOffset.UtcNow;

        var newVersion = new VersionInfo { SequenceNumber = 3, Timestamp = newTime };
        var existingVersion = new VersionInfo { SequenceNumber = 3, Timestamp = existingTime };

        var result = _detector.IsNewerVersion(newVersion, existingVersion);

        result.Should().BeTrue();
    }
}
