using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using DistributedWorkerService.Configuration;
using DistributedWorkerService.Models;
using DistributedWorkerService.Services;

namespace DistributedWorkerService.Tests;

public class KeyRangeProcessingServiceTests
{
    private readonly Mock<IExternalApiClient> _externalApiClientMock;
    private readonly Mock<ICosmosLeaseCheckpointService> _leaseCheckpointServiceMock;
    private readonly Mock<ILogger<KeyRangeProcessingService>> _loggerMock;
    private readonly WorkerConfiguration _config;
    private readonly KeyRangeProcessingService _service;

    public KeyRangeProcessingServiceTests()
    {
        _externalApiClientMock = new Mock<IExternalApiClient>();
        _leaseCheckpointServiceMock = new Mock<ICosmosLeaseCheckpointService>();
        _loggerMock = new Mock<ILogger<KeyRangeProcessingService>>();
        _config = new WorkerConfiguration();

        var configOptions = Options.Create(_config);
        _service = new KeyRangeProcessingService(
            _externalApiClientMock.Object,
            _leaseCheckpointServiceMock.Object,
            configOptions,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessKeyRangeAsync_ShouldReturnSuccessfulBatch_WhenNoChanges()
    {
        // Arrange
        var keyRangeId = "test-range";
        var workerId = "test-worker";
        var changesResponse = new ChangesResponse
        {
            KeyRangeId = keyRangeId,
            Changes = new List<ChangeData>()
        };

        _externalApiClientMock
            .Setup(x => x.GetChangesAsync(keyRangeId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(changesResponse);

        // Act
        var result = await _service.ProcessKeyRangeAsync(keyRangeId, workerId);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Changes);
        Assert.Empty(result.Errors);
        Assert.Equal(keyRangeId, result.KeyRangeId);
    }

    [Fact]
    public async Task ProcessKeyRangeAsync_ShouldProcessChanges_WhenChangesExist()
    {
        // Arrange
        var keyRangeId = "test-range";
        var workerId = "test-worker";
        var changes = new List<ChangeData>
        {
            new()
            {
                ChangeId = "change1",
                Timestamp = DateTime.UtcNow,
                Offset = "offset1",
                Operation = "create",
                EntityId = "entity1",
                EntityType = "TestEntity"
            },
            new()
            {
                ChangeId = "change2",
                Timestamp = DateTime.UtcNow.AddSeconds(1),
                Offset = "offset2",
                Operation = "update",
                EntityId = "entity2",
                EntityType = "TestEntity"
            }
        };

        var changesResponse = new ChangesResponse
        {
            KeyRangeId = keyRangeId,
            Changes = changes
        };

        _externalApiClientMock
            .Setup(x => x.GetChangesAsync(keyRangeId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(changesResponse);

        _leaseCheckpointServiceMock
            .Setup(x => x.UpdateCheckpointAndRenewLeaseAsync(
                keyRangeId, workerId, It.IsAny<CheckpointData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProcessKeyRangeAsync(keyRangeId, workerId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Changes.Count);
        Assert.Empty(result.Errors);
        Assert.Equal("offset2", result.Checkpoint.LastProcessedOffset);
    }

    [Fact]
    public async Task ProcessKeyRangeAsync_ShouldHandleCheckpointFailure()
    {
        // Arrange
        var keyRangeId = "test-range";
        var workerId = "test-worker";
        var changes = new List<ChangeData>
        {
            new()
            {
                ChangeId = "change1",
                Timestamp = DateTime.UtcNow,
                Offset = "offset1",
                Operation = "create",
                EntityId = "entity1",
                EntityType = "TestEntity"
            }
        };

        var changesResponse = new ChangesResponse
        {
            KeyRangeId = keyRangeId,
            Changes = changes
        };

        _externalApiClientMock
            .Setup(x => x.GetChangesAsync(keyRangeId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(changesResponse);

        _leaseCheckpointServiceMock
            .Setup(x => x.UpdateCheckpointAndRenewLeaseAsync(
                keyRangeId, workerId, It.IsAny<CheckpointData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Checkpoint update fails

        // Act
        var result = await _service.ProcessKeyRangeAsync(keyRangeId, workerId);

        // Assert
        Assert.False(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("Failed to update checkpoint", result.Errors[0]);
    }

    [Fact]
    public async Task ProcessKeyRangeAsync_ShouldStartFromLastCheckpoint()
    {
        // Arrange
        var keyRangeId = "test-range";
        var workerId = "test-worker";
        var lastCheckpoint = new CheckpointData
        {
            KeyRangeId = keyRangeId,
            LastProcessedOffset = "previous-offset",
            LastProcessedTimestamp = DateTime.UtcNow.AddMinutes(-1)
        };

        var changesResponse = new ChangesResponse
        {
            KeyRangeId = keyRangeId,
            Changes = new List<ChangeData>()
        };

        _externalApiClientMock
            .Setup(x => x.GetChangesAsync(keyRangeId, "previous-offset", It.IsAny<CancellationToken>()))
            .ReturnsAsync(changesResponse);

        // Act
        var result = await _service.ProcessKeyRangeAsync(keyRangeId, workerId, lastCheckpoint);

        // Assert
        _externalApiClientMock.Verify(
            x => x.GetChangesAsync(keyRangeId, "previous-offset", It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.True(result.Success);
    }
}