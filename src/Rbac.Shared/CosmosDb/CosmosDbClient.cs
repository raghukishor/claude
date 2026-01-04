using Microsoft.Azure.Cosmos;

namespace Rbac.Shared.CosmosDb;

/// <summary>
/// Implementation of ICosmosDbClient using the CosmosDB SDK.
/// </summary>
public class CosmosDbClient : ICosmosDbClient, IDisposable
{
    private readonly CosmosClient _client;
    private readonly string _databaseName;
    private Database? _database;
    private bool _disposed;

    public CosmosDbClient(CosmosDbSettings settings)
    {
        var options = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            },
            ConnectionMode = ConnectionMode.Gateway // Use Gateway for emulator compatibility
        };

        _client = new CosmosClient(settings.Endpoint, settings.Key, options);
        _databaseName = settings.DatabaseName;
    }

    public CosmosDbClient(CosmosClient client, string databaseName)
    {
        _client = client;
        _databaseName = databaseName;
    }

    public Container GetContainer(string containerName)
    {
        return _client.GetContainer(_databaseName, containerName);
    }

    public async Task<Database> CreateDatabaseIfNotExistsAsync(string databaseName)
    {
        var response = await _client.CreateDatabaseIfNotExistsAsync(databaseName);
        _database = response.Database;
        return _database;
    }

    public async Task<Container> CreateContainerIfNotExistsAsync(
        string databaseName,
        string containerName,
        string partitionKeyPath,
        int? throughput = null)
    {
        var database = _client.GetDatabase(databaseName);

        var containerProperties = new ContainerProperties(containerName, partitionKeyPath)
        {
            DefaultTimeToLive = -1 // Enable TTL but don't set default
        };

        var response = await database.CreateContainerIfNotExistsAsync(
            containerProperties,
            throughput);

        return response.Container;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _client.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
