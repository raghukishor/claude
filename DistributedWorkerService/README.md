# Distributed Worker Service

A robust .NET 8 Worker Service designed for distributed processing of changes from an external API. The service implements lease-based coordination, checkpointing, and fault tolerance across multiple worker instances.

## Features

- **Distributed Processing**: Multiple worker instances coordinate work using lease-based distribution
- **Fault Tolerance**: Automatic failover when workers fail, with configurable retry policies
- **Checkpointing**: Unified checkpoint and lease management in Cosmos DB
- **OAuth 2.0 Authentication**: Secure authentication with automatic token refresh
- **Circuit Breaker**: Resilience patterns for external API calls
- **Health Checks**: Built-in health monitoring for dependencies
- **Containerized**: Docker support for easy deployment

## Architecture

### Core Components

1. **DistributedChangeProcessorWorker**: Main background service orchestrating the processing
2. **CosmosLeaseCheckpointService**: Manages leases and checkpoints in Cosmos DB
3. **ExternalApiClient**: Handles OAuth 2.0 authenticated API calls with resilience
4. **KeyRangeProcessingService**: Processes individual key ranges with checkpoint updates

### Data Flow

1. Worker instances discover available tasks from external API
2. Workers compete for leases on key ranges using Cosmos DB
3. Each worker processes assigned key ranges incrementally
4. Checkpoints are updated frequently during processing
5. Leases are renewed periodically to maintain ownership
6. Failed workers automatically release leases for reassignment

## Configuration

### appsettings.json

```json
{
  "WorkerConfiguration": {
    "WorkerId": "worker-{HOSTNAME}-{GUID}",
    "PollingIntervalSeconds": 30,
    "LeaseDurationMinutes": 5,
    "CheckpointUpdateIntervalSeconds": 30,
    "MaxConcurrentKeyRanges": 10,
    "RetryPolicy": {
      "MaxRetryAttempts": 3,
      "BaseDelaySeconds": 2,
      "MaxDelaySeconds": 60
    }
  },
  "CosmosDb": {
    "ConnectionString": "your-cosmos-connection-string",
    "DatabaseName": "DistributedWorker",
    "ContainerName": "LeaseCheckpoints",
    "ThroughputRU": 400
  },
  "ExternalApi": {
    "BaseUrl": "https://api.example.com",
    "MetadataEndpoint": "/v1/tasks",
    "GetChangesEndpoint": "/v1/changes/{taskId}",
    "OAuth": {
      "TokenEndpoint": "https://auth.example.com/oauth/token",
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret",
      "Scope": "api.read"
    },
    "TimeoutSeconds": 30,
    "RateLimitRequestsPerSecond": 10
  }
}
```

### Environment-Specific Configuration

Create `appsettings.Development.json`, `appsettings.Staging.json`, etc. for environment-specific settings.

## Setup and Deployment

### Prerequisites

- .NET 8.0 SDK
- Azure Cosmos DB account
- External API with OAuth 2.0 support

### Local Development

1. **Clone and build**:
   ```bash
   git clone <repository-url>
   cd DistributedWorkerService
   dotnet restore
   dotnet build
   ```

2. **Configure settings**:
   - Update `appsettings.Development.json` with your Cosmos DB and API settings
   - Store secrets using .NET User Secrets:
     ```bash
     dotnet user-secrets set "CosmosDb:ConnectionString" "your-connection-string"
     dotnet user-secrets set "ExternalApi:OAuth:ClientSecret" "your-client-secret"
     ```

3. **Run locally**:
   ```bash
   dotnet run
   ```

### Docker Deployment

1. **Build image**:
   ```bash
   docker build -t distributed-worker-service .
   ```

2. **Run container**:
   ```bash
   docker run -d \
     --name worker-service \
     -e CosmosDb__ConnectionString="your-connection-string" \
     -e ExternalApi__OAuth__ClientSecret="your-client-secret" \
     distributed-worker-service
   ```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: distributed-worker-service
spec:
  replicas: 3
  selector:
    matchLabels:
      app: distributed-worker-service
  template:
    metadata:
      labels:
        app: distributed-worker-service
    spec:
      containers:
      - name: worker
        image: distributed-worker-service:latest
        env:
        - name: CosmosDb__ConnectionString
          valueFrom:
            secretKeyRef:
              name: worker-secrets
              key: cosmos-connection-string
        - name: ExternalApi__OAuth__ClientSecret
          valueFrom:
            secretKeyRef:
              name: worker-secrets
              key: api-client-secret
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          exec:
            command:
            - dotnet
            - DistributedWorkerService.dll
            - --health-check
          initialDelaySeconds: 30
          periodSeconds: 30
```

## Configuration Options

### Worker Configuration

- **WorkerId**: Unique identifier for the worker instance (supports templates)
- **PollingIntervalSeconds**: How often to check for new work
- **LeaseDurationMinutes**: How long leases are held
- **MaxConcurrentKeyRanges**: Maximum key ranges processed simultaneously
- **CheckpointUpdateIntervalSeconds**: How often to update checkpoints

### Templates in WorkerId

- `{HOSTNAME}`: Replaced with machine hostname
- `{GUID}`: Replaced with a short GUID
- `{PID}`: Replaced with process ID

## Monitoring and Observability

### Health Checks

The service includes health checks for:
- Cosmos DB connectivity
- External API connectivity

Access health status:
```bash
# Run with health check flag
dotnet DistributedWorkerService.dll --health-check
```

### Logging

Structured logging with correlation IDs for tracing requests across the distributed system.

Log levels:
- **Information**: Normal operation events
- **Debug**: Detailed processing information  
- **Warning**: Recoverable errors and retries
- **Error**: Failures requiring attention

### Metrics

Key metrics to monitor:
- Active worker count
- Lease acquisition rate
- Processing throughput
- Error rates
- Checkpoint update frequency

## Testing

### Run Tests

```bash
dotnet test
```

### Test Coverage

- Unit tests for core services
- Integration tests for Cosmos DB operations
- Mock tests for external API interactions

## Troubleshooting

### Common Issues

1. **Lease conflicts**: Multiple workers competing for the same key range
   - **Solution**: Check worker IDs are unique, verify clock synchronization

2. **Checkpoint update failures**: Workers losing leases during processing
   - **Solution**: Reduce checkpoint update interval, increase lease duration

3. **External API rate limiting**: Too many concurrent requests
   - **Solution**: Adjust `RateLimitRequestsPerSecond` setting

4. **Cosmos DB throttling**: Exceeding provisioned throughput
   - **Solution**: Increase RU/s or implement backoff strategies

### Debug Mode

Run with detailed logging:
```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.