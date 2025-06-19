using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DistributedWorkerService.Configuration;
using DistributedWorkerService.Models;

namespace DistributedWorkerService.Services;

/// <summary>
/// Service for processing individual key ranges with checkpoint management
/// </summary>
public interface IKeyRangeProcessingService
{
    Task<ProcessedChangesBatch> ProcessKeyRangeAsync(string keyRangeId, string workerId, CheckpointData? lastCheckpoint = null, CancellationToken cancellationToken = default);
}

public class KeyRangeProcessingService : IKeyRangeProcessingService
{
    private readonly IExternalApiClient _externalApiClient;
    private readonly ICosmosLeaseCheckpointService _leaseCheckpointService;
    private readonly WorkerConfiguration _config;
    private readonly ILogger<KeyRangeProcessingService> _logger;

    public KeyRangeProcessingService(
        IExternalApiClient externalApiClient,
        ICosmosLeaseCheckpointService leaseCheckpointService,
        IOptions<WorkerConfiguration> config,
        ILogger<KeyRangeProcessingService> logger)
    {
        _externalApiClient = externalApiClient;
        _leaseCheckpointService = leaseCheckpointService;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<ProcessedChangesBatch> ProcessKeyRangeAsync(string keyRangeId, string workerId, CheckpointData? lastCheckpoint = null, CancellationToken cancellationToken = default)
    {
        var batch = new ProcessedChangesBatch
        {
            KeyRangeId = keyRangeId,
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Processing key range {KeyRangeId} from checkpoint {Checkpoint}",
                keyRangeId, lastCheckpoint?.LastProcessedOffset ?? "start");

            // Get changes from external API starting from last checkpoint
            var changesResponse = await _externalApiClient.GetChangesAsync(
                keyRangeId, 
                lastCheckpoint?.LastProcessedOffset, 
                cancellationToken);

            if (changesResponse.Changes.Count == 0)
            {
                _logger.LogDebug("No changes found for key range {KeyRangeId}", keyRangeId);
                batch.Success = true;
                return batch;
            }

            _logger.LogInformation("Retrieved {ChangeCount} changes for key range {KeyRangeId}",
                changesResponse.Changes.Count, keyRangeId);

            // Process changes in smaller batches to update checkpoints frequently
            const int batchSize = 100;
            var processedChanges = new List<ChangeData>();
            var lastProcessedOffset = lastCheckpoint?.LastProcessedOffset;
            var lastProcessedTimestamp = lastCheckpoint?.LastProcessedTimestamp;

            for (int i = 0; i < changesResponse.Changes.Count; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentBatch = changesResponse.Changes.Skip(i).Take(batchSize).ToList();
                
                // Process the batch
                var processingResult = await ProcessChangesBatch(currentBatch, keyRangeId, cancellationToken);
                
                if (!processingResult.Success)
                {
                    batch.Errors.AddRange(processingResult.Errors);
                    _logger.LogError("Failed to process batch {BatchStart}-{BatchEnd} for key range {KeyRangeId}",
                        i, Math.Min(i + batchSize - 1, changesResponse.Changes.Count - 1), keyRangeId);
                    break;
                }

                processedChanges.AddRange(currentBatch);

                // Update checkpoint after each batch
                if (currentBatch.Count > 0)
                {
                    var lastChange = currentBatch.Last();
                    lastProcessedOffset = lastChange.Offset;
                    lastProcessedTimestamp = lastChange.Timestamp;

                    var checkpoint = new CheckpointData
                    {
                        KeyRangeId = keyRangeId,
                        LastProcessedOffset = lastProcessedOffset,
                        LastProcessedTimestamp = lastProcessedTimestamp,
                        ProcessedCount = processedChanges.Count
                    };

                    var checkpointUpdated = await _leaseCheckpointService.UpdateCheckpointAndRenewLeaseAsync(
                        keyRangeId, workerId, checkpoint, cancellationToken);

                    if (!checkpointUpdated)
                    {
                        _logger.LogWarning("Failed to update checkpoint for key range {KeyRangeId} - lease may have expired",
                            keyRangeId);
                        batch.Errors.Add("Failed to update checkpoint - lease expired");
                        break;
                    }

                    _logger.LogDebug("Checkpoint updated for key range {KeyRangeId} after processing {ProcessedCount} changes",
                        keyRangeId, processedChanges.Count);
                }
            }

            // Final results
            batch.Changes = processedChanges;
            batch.Success = batch.Errors.Count == 0;
            batch.Checkpoint = new CheckpointData
            {
                KeyRangeId = keyRangeId,
                LastProcessedOffset = lastProcessedOffset,
                LastProcessedTimestamp = lastProcessedTimestamp,
                ProcessedCount = processedChanges.Count
            };

            _logger.LogInformation("Completed processing key range {KeyRangeId} - processed {ProcessedCount} changes, success: {Success}",
                keyRangeId, processedChanges.Count, batch.Success);

            return batch;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Processing cancelled for key range {KeyRangeId}", keyRangeId);
            batch.Errors.Add("Operation cancelled");
            return batch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing key range {KeyRangeId}", keyRangeId);
            batch.Errors.Add($"Processing error: {ex.Message}");
            return batch;
        }
    }

    private async Task<(bool Success, List<string> Errors)> ProcessChangesBatch(List<ChangeData> changes, string keyRangeId, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        try
        {
            _logger.LogDebug("Processing batch of {ChangeCount} changes for key range {KeyRangeId}",
                changes.Count, keyRangeId);

            // Process each change in the batch
            foreach (var change in changes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await ProcessSingleChange(change, cancellationToken);
                }
                catch (Exception ex)
                {
                    var error = $"Failed to process change {change.ChangeId}: {ex.Message}";
                    errors.Add(error);
                    _logger.LogError(ex, "Error processing change {ChangeId} in key range {KeyRangeId}",
                        change.ChangeId, keyRangeId);

                    // Decide whether to continue or fail the entire batch
                    // For now, we'll continue processing other changes but log the error
                }
            }

            return (errors.Count == 0, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Batch processing error: {ex.Message}");
            _logger.LogError(ex, "Error processing batch for key range {KeyRangeId}", keyRangeId);
            return (false, errors);
        }
    }

    private async Task ProcessSingleChange(ChangeData change, CancellationToken cancellationToken)
    {
        // This is where you would implement your business logic for processing individual changes
        // For this example, we'll just log the change processing
        
        _logger.LogDebug("Processing change {ChangeId} - Operation: {Operation}, Entity: {EntityType}/{EntityId}",
            change.ChangeId, change.Operation, change.EntityType, change.EntityId);

        // Simulate processing time
        await Task.Delay(10, cancellationToken);

        // Example business logic based on operation type
        switch (change.Operation.ToLowerInvariant())
        {
            case "create":
                await ProcessCreateOperation(change, cancellationToken);
                break;
            case "update":
                await ProcessUpdateOperation(change, cancellationToken);
                break;
            case "delete":
                await ProcessDeleteOperation(change, cancellationToken);
                break;
            default:
                _logger.LogWarning("Unknown operation type {Operation} for change {ChangeId}",
                    change.Operation, change.ChangeId);
                break;
        }

        _logger.LogTrace("Completed processing change {ChangeId}", change.ChangeId);
    }

    private async Task ProcessCreateOperation(ChangeData change, CancellationToken cancellationToken)
    {
        // Implement create operation logic
        _logger.LogDebug("Processing CREATE operation for {EntityType} {EntityId}",
            change.EntityType, change.EntityId);
        
        // Example: Save to database, send to message queue, etc.
        await Task.CompletedTask;
    }

    private async Task ProcessUpdateOperation(ChangeData change, CancellationToken cancellationToken)
    {
        // Implement update operation logic
        _logger.LogDebug("Processing UPDATE operation for {EntityType} {EntityId}",
            change.EntityType, change.EntityId);
        
        // Example: Update database record, invalidate cache, etc.
        await Task.CompletedTask;
    }

    private async Task ProcessDeleteOperation(ChangeData change, CancellationToken cancellationToken)
    {
        // Implement delete operation logic
        _logger.LogDebug("Processing DELETE operation for {EntityType} {EntityId}",
            change.EntityType, change.EntityId);
        
        // Example: Remove from database, cleanup related data, etc.
        await Task.CompletedTask;
    }
}