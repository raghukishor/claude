using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using DistributedWorkerService.Configuration;
using DistributedWorkerService.Models;
using DistributedWorkerService.Services;

namespace DistributedWorkerService.Tests;

public class CosmosLeaseCheckpointServiceTests
{
    private readonly Mock<ILogger<CosmosLeaseCheckpointService>> _loggerMock;
    private readonly CosmosDbConfiguration _config;

    public CosmosLeaseCheckpointServiceTests()
    {
        _loggerMock = new Mock<ILogger<CosmosLeaseCheckpointService>>();
        _config = new CosmosDbConfiguration
        {
            ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            DatabaseName = "TestDatabase",
            ContainerName = "TestContainer"
        };
    }

    [Fact]
    public void LeaseCheckpointDocument_IsLeaseValid_ShouldReturnTrue_WhenLeaseIsActive()
    {
        // Arrange
        var workerId = "test-worker";
        var document = new LeaseCheckpointDocument
        {
            WorkerId = workerId,
            LeaseExpiry = DateTime.UtcNow.AddMinutes(5),
            ProcessingState = ProcessingState.InProgress
        };

        // Act
        var result = document.IsLeaseValid(workerId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void LeaseCheckpointDocument_IsLeaseValid_ShouldReturnFalse_WhenLeaseExpired()
    {
        // Arrange
        var workerId = "test-worker";
        var document = new LeaseCheckpointDocument
        {
            WorkerId = workerId,
            LeaseExpiry = DateTime.UtcNow.AddMinutes(-1), // Expired
            ProcessingState = ProcessingState.InProgress
        };

        // Act
        var result = document.IsLeaseValid(workerId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void LeaseCheckpointDocument_IsLeaseValid_ShouldReturnFalse_WhenWrongWorker()
    {
        // Arrange
        var workerId = "test-worker";
        var document = new LeaseCheckpointDocument
        {
            WorkerId = "different-worker",
            LeaseExpiry = DateTime.UtcNow.AddMinutes(5),
            ProcessingState = ProcessingState.InProgress
        };

        // Act
        var result = document.IsLeaseValid(workerId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void LeaseCheckpointDocument_IsLeaseExpired_ShouldReturnTrue_WhenNoExpiry()
    {
        // Arrange
        var document = new LeaseCheckpointDocument
        {
            LeaseExpiry = null,
            ProcessingState = ProcessingState.Available
        };

        // Act
        var result = document.IsLeaseExpired();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void LeaseCheckpointDocument_IsLeaseExpired_ShouldReturnTrue_WhenExpired()
    {
        // Arrange
        var document = new LeaseCheckpointDocument
        {
            LeaseExpiry = DateTime.UtcNow.AddMinutes(-1),
            ProcessingState = ProcessingState.InProgress
        };

        // Act
        var result = document.IsLeaseExpired();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void LeaseCheckpointDocument_IsLeaseExpired_ShouldReturnFalse_WhenActive()
    {
        // Arrange
        var document = new LeaseCheckpointDocument
        {
            LeaseExpiry = DateTime.UtcNow.AddMinutes(5),
            ProcessingState = ProcessingState.InProgress
        };

        // Act
        var result = document.IsLeaseExpired();

        // Assert
        Assert.False(result);
    }
}