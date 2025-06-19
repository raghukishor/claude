using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using DistributedWorkerService.Configuration;
using DistributedWorkerService.Models;

namespace DistributedWorkerService.Services;

/// <summary>
/// Main background service that orchestrates distributed change processing
/// </summary>
public class DistributedChangeProcessorWorker : BackgroundService
{
    private readonly ICosmosLeaseCheckpointService _leaseCheckpointService;
    private readonly IExternalApiClient _externalApiClient;
    private readonly IKeyRangeProcessingService _keyRangeProcessingService;
    private readonly WorkerConfiguration _config;
    private readonly ILogger<DistributedChangeProcessorWorker> _logger;

    private readonly string _workerId;
    private readonly ConcurrentDictionary<string, Task> _activeProcessingTasks = new();
    private readonly SemaphoreSlim _concurrencyControl;
    private readonly Timer _leaseRenewalTimer;
    private readonly ConcurrentDictionary<string, DateTime> _ownedLeases = new();

    public DistributedChangeProcessorWorker(
        ICosmosLeaseCheckpointService leaseCheckpointService,
        IExternalApiClient externalApiClient,
        IKeyRangeProcessingService keyRangeProcessingService,
        IOptions<WorkerConfiguration> config,
        ILogger<DistributedChangeProcessorWorker> logger)
    {
        _leaseCheckpointService = leaseCheckpointService;
        _externalApiClient = externalApiClient;
        _keyRangeProcessingService = keyRangeProcessingService;
        _config = config.Value;
        _logger = logger;

        _workerId = _config.ResolveWorkerId();
        _concurrencyControl = new SemaphoreSlim(_config.MaxConcurrentKeyRanges);

        // Timer for lease renewal
        _leaseRenewalTimer = new Timer(RenewLeases, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DistributedChangeProcessorWorker with ID: {WorkerId}", _workerId);

        try
        {
            await _leaseCheckpointService.InitializeAsync(cancellationToken);
            _logger.LogInformation("Cosmos DB lease checkpoint service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Cosmos DB lease checkpoint service");
            throw;
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping DistributedChangeProcessorWorker {WorkerId}", _workerId);

        // Stop the lease renewal timer
        await _leaseRenewalTimer.DisposeAsync();

        // Wait for all active processing tasks to complete
        var activeTasks = _activeProcessingTasks.Values.ToArray();
        if (activeTasks.Length > 0)
        {
            _logger.LogInformation("Waiting for {TaskCount} active processing tasks to complete", activeTasks.Length);
            await Task.WhenAll(activeTasks);
        }

        // Release all owned leases
        await ReleaseAllLeasesAsync();

        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DistributedChangeProcessorWorker {WorkerId} started execution", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessingCycle(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker execution cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in processing cycle, continuing after delay");
            }

            // Wait before next processing cycle
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.PollingIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("DistributedChangeProcessorWorker {WorkerId} execution completed", _workerId);
    }

    private async Task ProcessingCycle(CancellationToken cancellationToken)
    {
        try
        {
            // Clean up completed tasks
            CleanupCompletedTasks();

            // Get available capacity
            var availableSlots = _config.MaxConcurrentKeyRanges - _activeProcessingTasks.Count;
            if (availableSlots <= 0)
            {
                _logger.LogDebug("All processing slots occupied ({ActiveTasks}/{MaxConcurrent})",
                    _activeProcessingTasks.Count, _config.MaxConcurrentKeyRanges);
                return;
            }

            // Get tasks metadata from external API
            var tasksResponse = await _externalApiClient.GetTasksAsync(cancellationToken);
            if (tasksResponse.Tasks.Count == 0)
            {
                _logger.LogDebug("No tasks available from external API");
                return;
            }

            _logger.LogDebug("Retrieved {TaskCount} tasks from external API", tasksResponse.Tasks.Count);

            // Try to acquire leases for available key ranges
            var acquiredLeases = new List<string>();
            var leaseDuration = TimeSpan.FromMinutes(_config.LeaseDurationMinutes);

            foreach (var task in tasksResponse.Tasks.Take(availableSlots))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Skip if already processing this key range
                if (_activeProcessingTasks.ContainsKey(task.KeyRangeId))
                    continue;

                // Try to acquire lease
                var leaseAcquired = await _leaseCheckpointService.TryAcquireLeaseAsync(
                    task.KeyRangeId, _workerId, leaseDuration, cancellationToken);

                if (leaseAcquired)
                {
                    acquiredLeases.Add(task.KeyRangeId);
                    _ownedLeases[task.KeyRangeId] = DateTime.UtcNow.Add(leaseDuration);
                    _logger.LogInformation("Acquired lease for key range {KeyRangeId}", task.KeyRangeId);
                }
            }

            // Start processing tasks for acquired leases
            foreach (var keyRangeId in acquiredLeases)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var processingTask = StartKeyRangeProcessing(keyRangeId, cancellationToken);
                _activeProcessingTasks[keyRangeId] = processingTask;
            }

            _logger.LogInformation("Processing cycle completed - Active tasks: {ActiveTasks}, Newly acquired: {NewLeases}",
                _activeProcessingTasks.Count, acquiredLeases.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in processing cycle");
            throw;
        }
    }

    private Task StartKeyRangeProcessing(string keyRangeId, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            await _concurrencyControl.WaitAsync(cancellationToken);
            try
            {
                _logger.LogInformation("Starting processing for key range {KeyRangeId}", keyRangeId);

                // Get existing checkpoint
                var checkpoint = await _leaseCheckpointService.GetCheckpointAsync(keyRangeId, cancellationToken);

                // Process the key range
                var result = await _keyRangeProcessingService.ProcessKeyRangeAsync(
                    keyRangeId, _workerId, checkpoint, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("Successfully processed key range {KeyRangeId} - {ProcessedCount} changes",
                        keyRangeId, result.Changes.Count);
                }
                else
                {
                    _logger.LogError("Failed to process key range {KeyRangeId} - Errors: {Errors}",
                        keyRangeId, string.Join(", ", result.Errors));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Processing cancelled for key range {KeyRangeId}", keyRangeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing key range {KeyRangeId}", keyRangeId);
            }
            finally
            {
                // Release lease when done processing
                try
                {
                    await _leaseCheckpointService.ReleaseLeaseAsync(keyRangeId, _workerId, CancellationToken.None);
                    _ownedLeases.TryRemove(keyRangeId, out _);
                    _logger.LogInformation("Released lease for key range {KeyRangeId}", keyRangeId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error releasing lease for key range {KeyRangeId}", keyRangeId);
                }

                _concurrencyControl.Release();
            }
        }, cancellationToken);
    }

    private void CleanupCompletedTasks()
    {
        var completedTasks = _activeProcessingTasks
            .Where(kvp => kvp.Value.IsCompleted)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var keyRangeId in completedTasks)
        {
            if (_activeProcessingTasks.TryRemove(keyRangeId, out var task))
            {
                if (task.IsFaulted)
                {
                    _logger.LogError(task.Exception, "Processing task for key range {KeyRangeId} faulted", keyRangeId);
                }
                task.Dispose();
            }
        }

        if (completedTasks.Count > 0)
        {
            _logger.LogDebug("Cleaned up {CompletedTaskCount} completed processing tasks", completedTasks.Count);
        }
    }

    private async void RenewLeases(object? state)
    {
        if (_ownedLeases.IsEmpty)
            return;

        var leasesToRenew = _ownedLeases.Keys.ToList();
        var renewalTasks = leasesToRenew.Select(async keyRangeId =>
        {
            try
            {
                var renewed = await _leaseCheckpointService.RenewLeaseAsync(keyRangeId, _workerId);
                if (renewed)
                {
                    _ownedLeases[keyRangeId] = DateTime.UtcNow.AddMinutes(_config.LeaseDurationMinutes);
                    _logger.LogDebug("Renewed lease for key range {KeyRangeId}", keyRangeId);
                }
                else
                {
                    _ownedLeases.TryRemove(keyRangeId, out _);
                    _logger.LogWarning("Failed to renew lease for key range {KeyRangeId}", keyRangeId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error renewing lease for key range {KeyRangeId}", keyRangeId);
                _ownedLeases.TryRemove(keyRangeId, out _);
            }
        });

        await Task.WhenAll(renewalTasks);
    }

    private async Task ReleaseAllLeasesAsync()
    {
        var leasesToRelease = _ownedLeases.Keys.ToList();
        var releaseTasks = leasesToRelease.Select(async keyRangeId =>
        {
            try
            {
                await _leaseCheckpointService.ReleaseLeaseAsync(keyRangeId, _workerId);
                _logger.LogInformation("Released lease for key range {KeyRangeId} during shutdown", keyRangeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing lease for key range {KeyRangeId} during shutdown", keyRangeId);
            }
        });

        await Task.WhenAll(releaseTasks);
        _ownedLeases.Clear();
    }

    public override void Dispose()
    {
        _leaseRenewalTimer?.Dispose();
        _concurrencyControl?.Dispose();
        base.Dispose();
    }
}