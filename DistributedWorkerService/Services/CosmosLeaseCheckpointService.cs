using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using DistributedWorkerService.Configuration;
using DistributedWorkerService.Models;

namespace DistributedWorkerService.Services;

/// <summary>
/// Service for managing leases and checkpoints in Cosmos DB
/// </summary>
public interface ICosmosLeaseCheckpointService
{
    Task<bool> TryAcquireLeaseAsync(string keyRangeId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
    Task<bool> RenewLeaseAsync(string keyRangeId, string workerId, CancellationToken cancellationToken = default);
    Task<bool> UpdateCheckpointAsync(string keyRangeId, string workerId, CheckpointData checkpoint, CancellationToken cancellationToken = default);
    Task<bool> UpdateCheckpointAndRenewLeaseAsync(string keyRangeId, string workerId, CheckpointData checkpoint, CancellationToken cancellationToken = default);
    Task<bool> ReleaseLeaseAsync(string keyRangeId, string workerId, CancellationToken cancellationToken = default);
    Task<List<string>> GetAvailableKeyRangesAsync(CancellationToken cancellationToken = default);
    Task<CheckpointData?> GetCheckpointAsync(string keyRangeId, CancellationToken cancellationToken = default);
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public class CosmosLeaseCheckpointService : ICosmosLeaseCheckpointService, IDisposable
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbConfiguration _config;
    private readonly ILogger<CosmosLeaseCheckpointService> _logger;
    private Container? _container;
    private bool _disposed;

    public CosmosLeaseCheckpointService(
        IOptions<CosmosDbConfiguration> config,
        ILogger<CosmosLeaseCheckpointService> logger)
    {
        _config = config.Value;
        _logger = logger;
        _cosmosClient = new CosmosClient(_config.ConnectionString);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                _config.DatabaseName,
                throughput: _config.ThroughputRU,
                cancellationToken: cancellationToken);

            _container = await database.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(_config.ContainerName, "/partitionKey")
                {
                    DefaultTimeToLive = 7 * 24 * 60 * 60 // 7 days
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation("Cosmos DB container initialized: {DatabaseName}/{ContainerName}",
                _config.DatabaseName, _config.ContainerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos DB container");
            throw;
        }
    }

    public async Task<bool> TryAcquireLeaseAsync(string keyRangeId, string workerId, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        if (_container == null)
            throw new InvalidOperationException("Service not initialized. Call InitializeAsync first.");

        try
        {
            var documentId = keyRangeId;
            var leaseExpiry = DateTime.UtcNow.Add(leaseDuration);

            // Try to read existing document
            try
            {
                var response = await _container.ReadItemAsync<LeaseCheckpointDocument>(
                    documentId, new PartitionKey(keyRangeId), cancellationToken: cancellationToken);

                var existingDoc = response.Resource;

                // Check if lease is available
                if (!existingDoc.IsLeaseExpired())
                {
                    _logger.LogDebug("Lease for key range {KeyRangeId} is not available, owned by {WorkerId} until {LeaseExpiry}",
                        keyRangeId, existingDoc.WorkerId, existingDoc.LeaseExpiry);
                    return false;
                }

                // Update existing document
                existingDoc.WorkerId = workerId;
                existingDoc.LeaseAcquiredAt = DateTime.UtcNow;
                existingDoc.LeaseExpiry = leaseExpiry;
                existingDoc.ProcessingState = ProcessingState.InProgress;

                await _container.UpsertItemAsync(existingDoc, new PartitionKey(keyRangeId),
                    new ItemRequestOptions { IfMatchEtag = existingDoc.ETag }, cancellationToken);

                _logger.LogInformation("Lease acquired for key range {KeyRangeId} by worker {WorkerId} until {LeaseExpiry}",
                    keyRangeId, workerId, leaseExpiry);
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Create new document
                var newDoc = new LeaseCheckpointDocument
                {
                    Id = documentId,
                    PartitionKey = keyRangeId,
                    KeyRangeId = keyRangeId,
                    WorkerId = workerId,
                    LeaseAcquiredAt = DateTime.UtcNow,
                    LeaseExpiry = leaseExpiry,
                    ProcessingState = ProcessingState.InProgress
                };

                await _container.CreateItemAsync(newDoc, new PartitionKey(keyRangeId), cancellationToken: cancellationToken);

                _logger.LogInformation("New lease created for key range {KeyRangeId} by worker {WorkerId} until {LeaseExpiry}",
                    keyRangeId, workerId, leaseExpiry);
                return true;
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            _logger.LogDebug("Lease acquisition failed due to concurrency conflict for key range {KeyRangeId}", keyRangeId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lease for key range {KeyRangeId}", keyRangeId);
            return false;
        }
    }

    public async Task<bool> RenewLeaseAsync(string keyRangeId, string workerId, CancellationToken cancellationToken = default)
    {
        if (_container == null)
            throw new InvalidOperationException("Service not initialized. Call InitializeAsync first.");

        try
        {
            var response = await _container.ReadItemAsync<LeaseCheckpointDocument>(
                keyRangeId, new PartitionKey(keyRangeId), cancellationToken: cancellationToken);

            var doc = response.Resource;

            if (!doc.IsLeaseValid(workerId))
            {
                _logger.LogWarning("Cannot renew lease for key range {KeyRangeId} - not owned by worker {WorkerId}",
                    keyRangeId, workerId);
                return false;
            }

            doc.LeaseExpiry = DateTime.UtcNow.AddMinutes(5); // Default lease duration

            await _container.UpsertItemAsync(doc, new PartitionKey(keyRangeId),
                new ItemRequestOptions { IfMatchEtag = doc.ETag }, cancellationToken);

            _logger.LogDebug("Lease renewed for key range {KeyRangeId} by worker {WorkerId} until {LeaseExpiry}",
                keyRangeId, workerId, doc.LeaseExpiry);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Cannot renew lease for key range {KeyRangeId} - document not found", keyRangeId);
            return false;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            _logger.LogDebug("Lease renewal failed due to concurrency conflict for key range {KeyRangeId}", keyRangeId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing lease for key range {KeyRangeId}", keyRangeId);
            return false;
        }
    }

    public async Task<bool> UpdateCheckpointAsync(string keyRangeId, string workerId, CheckpointData checkpoint, CancellationToken cancellationToken = default)
    {
        if (_container == null)
            throw new InvalidOperationException("Service not initialized. Call InitializeAsync first.");

        try
        {
            var response = await _container.ReadItemAsync<LeaseCheckpointDocument>(
                keyRangeId, new PartitionKey(keyRangeId), cancellationToken: cancellationToken);

            var doc = response.Resource;

            if (!doc.IsLeaseValid(workerId))
            {
                _logger.LogWarning("Cannot update checkpoint for key range {KeyRangeId} - not owned by worker {WorkerId}",
                    keyRangeId, workerId);
                return false;
            }

            doc.LastProcessedTimestamp = checkpoint.LastProcessedTimestamp;
            doc.LastProcessedOffset = checkpoint.LastProcessedOffset;
            doc.LastCheckpointUpdate = DateTime.UtcNow;

            await _container.UpsertItemAsync(doc, new PartitionKey(keyRangeId),
                new ItemRequestOptions { IfMatchEtag = doc.ETag }, cancellationToken);

            _logger.LogDebug("Checkpoint updated for key range {KeyRangeId} - offset: {Offset}, timestamp: {Timestamp}",
                keyRangeId, checkpoint.LastProcessedOffset, checkpoint.LastProcessedTimestamp);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Cannot update checkpoint for key range {KeyRangeId} - document not found", keyRangeId);
            return false;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            _logger.LogDebug("Checkpoint update failed due to concurrency conflict for key range {KeyRangeId}", keyRangeId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating checkpoint for key range {KeyRangeId}", keyRangeId);
            return false;
        }
    }

    public async Task<bool> UpdateCheckpointAndRenewLeaseAsync(string keyRangeId, string workerId, CheckpointData checkpoint, CancellationToken cancellationToken = default)
    {
        if (_container == null)
            throw new InvalidOperationException("Service not initialized. Call InitializeAsync first.");

        try
        {
            var response = await _container.ReadItemAsync<LeaseCheckpointDocument>(
                keyRangeId, new PartitionKey(keyRangeId), cancellationToken: cancellationToken);

            var doc = response.Resource;

            if (!doc.IsLeaseValid(workerId))
            {
                _logger.LogWarning("Cannot update checkpoint and renew lease for key range {KeyRangeId} - not owned by worker {WorkerId}",
                    keyRangeId, workerId);
                return false;
            }

            // Atomic update: checkpoint + lease renewal
            doc.LastProcessedTimestamp = checkpoint.LastProcessedTimestamp;
            doc.LastProcessedOffset = checkpoint.LastProcessedOffset;
            doc.LastCheckpointUpdate = DateTime.UtcNow;
            doc.LeaseExpiry = DateTime.UtcNow.AddMinutes(5);

            await _container.UpsertItemAsync(doc, new PartitionKey(keyRangeId),
                new ItemRequestOptions { IfMatchEtag = doc.ETag }, cancellationToken);

            _logger.LogDebug("Checkpoint updated and lease renewed for key range {KeyRangeId}", keyRangeId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating checkpoint and renewing lease for key range {KeyRangeId}", keyRangeId);
            return false;
        }
    }

    public async Task<bool> ReleaseLeaseAsync(string keyRangeId, string workerId, CancellationToken cancellationToken = default)
    {
        if (_container == null)
            throw new InvalidOperationException("Service not initialized. Call InitializeAsync first.");

        try
        {
            var response = await _container.ReadItemAsync<LeaseCheckpointDocument>(
                keyRangeId, new PartitionKey(keyRangeId), cancellationToken: cancellationToken);

            var doc = response.Resource;

            if (doc.WorkerId != workerId)
            {
                _logger.LogWarning("Cannot release lease for key range {KeyRangeId} - not owned by worker {WorkerId}",
                    keyRangeId, workerId);
                return false;
            }

            doc.WorkerId = null;
            doc.LeaseExpiry = null;
            doc.LeaseAcquiredAt = null;
            doc.ProcessingState = ProcessingState.Available;

            await _container.UpsertItemAsync(doc, new PartitionKey(keyRangeId),
                new ItemRequestOptions { IfMatchEtag = doc.ETag }, cancellationToken);

            _logger.LogInformation("Lease released for key range {KeyRangeId} by worker {WorkerId}", keyRangeId, workerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lease for key range {KeyRangeId}", keyRangeId);
            return false;
        }
    }

    public async Task<List<string>> GetAvailableKeyRangesAsync(CancellationToken cancellationToken = default)
    {
        if (_container == null)
            throw new InvalidOperationException("Service not initialized. Call InitializeAsync first.");

        try
        {
            var query = "SELECT c.keyRangeId FROM c WHERE c.documentType = 'LeaseCheckpoint' AND (c.leaseExpiry <= GetCurrentDateTime() OR c.leaseExpiry = null OR c.processingState = 'Available')";
            
            var iterator = _container.GetItemQueryIterator<dynamic>(query);
            var keyRanges = new List<string>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                keyRanges.AddRange(response.Select(item => (string)item.keyRangeId));
            }

            return keyRanges.Distinct().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available key ranges");
            return new List<string>();
        }
    }

    public async Task<CheckpointData?> GetCheckpointAsync(string keyRangeId, CancellationToken cancellationToken = default)
    {
        if (_container == null)
            throw new InvalidOperationException("Service not initialized. Call InitializeAsync first.");

        try
        {
            var response = await _container.ReadItemAsync<LeaseCheckpointDocument>(
                keyRangeId, new PartitionKey(keyRangeId), cancellationToken: cancellationToken);

            var doc = response.Resource;
            return new CheckpointData
            {
                KeyRangeId = doc.KeyRangeId,
                LastProcessedTimestamp = doc.LastProcessedTimestamp,
                LastProcessedOffset = doc.LastProcessedOffset,
                LastCheckpointUpdate = doc.LastCheckpointUpdate ?? DateTime.MinValue
            };
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checkpoint for key range {KeyRangeId}", keyRangeId);
            return null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cosmosClient?.Dispose();
            _disposed = true;
        }
    }
}