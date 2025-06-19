using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DistributedWorkerService.Configuration;
using DistributedWorkerService.Models;

namespace DistributedWorkerService.Services;

/// <summary>
/// Client for external API with OAuth 2.0 authentication and resilience policies
/// </summary>
public interface IExternalApiClient
{
    Task<TaskMetadataResponse> GetTasksAsync(CancellationToken cancellationToken = default);
    Task<ChangesResponse> GetChangesAsync(string taskId, string? fromCheckpoint = null, CancellationToken cancellationToken = default);
}

public class ExternalApiClient : IExternalApiClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ExternalApiConfiguration _config;
    private readonly ILogger<ExternalApiClient> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly SemaphoreSlim _rateLimitSemaphore;
    private readonly Timer _rateLimitTimer;
    
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _tokenSemaphore = new(1, 1);
    private bool _disposed;

    public ExternalApiClient(
        HttpClient httpClient,
        IOptions<ExternalApiConfiguration> config,
        ILogger<ExternalApiClient> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);

        // Rate limiting
        _rateLimitSemaphore = new SemaphoreSlim(_config.RateLimitRequestsPerSecond, _config.RateLimitRequestsPerSecond);
        _rateLimitTimer = new Timer(_ => 
        {
            var releases = Math.Min(_config.RateLimitRequestsPerSecond - _rateLimitSemaphore.CurrentCount, _config.RateLimitRequestsPerSecond);
            if (releases > 0)
                _rateLimitSemaphore.Release(releases);
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        // Simple resilience pipeline with retry
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new()
            {
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(),
                Delay = TimeSpan.FromSeconds(2),
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning("Retrying request (attempt {Attempt}): {Exception}",
                        args.AttemptNumber + 1, args.Outcome.Exception?.Message ?? "HTTP error");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<TaskMetadataResponse> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        await EnsureRateLimitAsync(cancellationToken);

        return await _resiliencePipeline.ExecuteAsync(async (ct) =>
        {
            await EnsureAuthenticatedAsync(ct);

            var request = new HttpRequestMessage(HttpMethod.Get, _config.MetadataEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            _logger.LogDebug("Fetching tasks from {Endpoint}", _config.MetadataEndpoint);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 Unauthorized - refreshing token and retrying");
                await RefreshTokenAsync(ct);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                response = await _httpClient.SendAsync(request, ct);
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<TaskMetadataResponse>(content, GetJsonOptions());

            _logger.LogDebug("Retrieved {TaskCount} tasks from API", result?.Tasks.Count ?? 0);
            return result ?? new TaskMetadataResponse();

        }, cancellationToken);
    }

    public async Task<ChangesResponse> GetChangesAsync(string taskId, string? fromCheckpoint = null, CancellationToken cancellationToken = default)
    {
        await EnsureRateLimitAsync(cancellationToken);

        return await _resiliencePipeline.ExecuteAsync(async (ct) =>
        {
            await EnsureAuthenticatedAsync(ct);

            var endpoint = _config.GetChangesEndpoint.Replace("{taskId}", taskId);
            var uri = new UriBuilder(_httpClient.BaseAddress + endpoint.TrimStart('/'));
            
            if (!string.IsNullOrEmpty(fromCheckpoint))
            {
                uri.Query = $"fromOffset={Uri.EscapeDataString(fromCheckpoint)}";
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uri.Uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            _logger.LogDebug("Fetching changes for task {TaskId} from checkpoint {Checkpoint}", taskId, fromCheckpoint);

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 Unauthorized - refreshing token and retrying");
                await RefreshTokenAsync(ct);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                response = await _httpClient.SendAsync(request, ct);
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ChangesResponse>(content, GetJsonOptions());

            _logger.LogDebug("Retrieved {ChangeCount} changes for task {TaskId}", 
                result?.Changes.Count ?? 0, taskId);
            return result ?? new ChangesResponse { KeyRangeId = taskId };

        }, cancellationToken);
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
            return;

        await _tokenSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check pattern
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
                return;

            await RefreshTokenAsync(cancellationToken);
        }
        finally
        {
            _tokenSemaphore.Release();
        }
    }

    private async Task RefreshTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Refreshing OAuth token from {TokenEndpoint}", _config.OAuth.TokenEndpoint);

            var tokenRequest = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "client_credentials"),
                new("client_id", _config.OAuth.ClientId),
                new("client_secret", _config.OAuth.ClientSecret),
                new("scope", _config.OAuth.Scope)
            };

            var request = new HttpRequestMessage(HttpMethod.Post, _config.OAuth.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(tokenRequest)
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(content, GetJsonOptions());

            if (tokenResponse?.AccessToken == null)
                throw new InvalidOperationException("Token response does not contain access token");

            _accessToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60); // 60 second buffer

            _logger.LogInformation("OAuth token refreshed successfully, expires at {Expiry}", _tokenExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh OAuth token");
            throw;
        }
    }

    private async Task EnsureRateLimitAsync(CancellationToken cancellationToken)
    {
        await _rateLimitSemaphore.WaitAsync(cancellationToken);
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _rateLimitTimer?.Dispose();
            _rateLimitSemaphore?.Dispose();
            _tokenSemaphore?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// OAuth token response model
/// </summary>
internal class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}