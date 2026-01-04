using Microsoft.Azure.Cosmos;
using Rbac.Shared.CosmosDb;

namespace Rbac.IntegrationTests.Fixtures;

/// <summary>
/// Fixture for setting up CosmosDB test containers.
/// Requires CosmosDB Emulator to be running on localhost:8081.
/// </summary>
public class CosmosDbFixture : IAsyncLifetime
{
    private CosmosClient? _client;
    private Database? _controlPlaneDb;
    private Database? _dataPlaneDb;

    public CosmosDbSettings ControlPlaneSettings { get; } = new()
    {
        Endpoint = "https://localhost:8081",
        Key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
        DatabaseName = $"RbacControlPlaneTest_{Guid.NewGuid():N}"
    };

    public CosmosDbSettings DataPlaneSettings { get; } = new()
    {
        Endpoint = "https://localhost:8081",
        Key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
        DatabaseName = $"RbacDataPlaneTest_{Guid.NewGuid():N}"
    };

    public async Task InitializeAsync()
    {
        var options = new CosmosClientOptions
        {
            HttpClientFactory = () =>
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                return new HttpClient(handler);
            },
            ConnectionMode = ConnectionMode.Gateway
        };

        _client = new CosmosClient(
            ControlPlaneSettings.Endpoint,
            ControlPlaneSettings.Key,
            options);

        // Create Control Plane database and containers
        _controlPlaneDb = await _client.CreateDatabaseIfNotExistsAsync(ControlPlaneSettings.DatabaseName);
        await _controlPlaneDb.CreateContainerIfNotExistsAsync(
            ContainerNames.RoleDefinitions,
            "/partitionKey");
        await _controlPlaneDb.CreateContainerIfNotExistsAsync(
            ContainerNames.RoleAssignments,
            "/scope");
        await _controlPlaneDb.CreateContainerIfNotExistsAsync(
            ContainerNames.SyncLeases,
            "/id");

        // Create Data Plane database and containers
        _dataPlaneDb = await _client.CreateDatabaseIfNotExistsAsync(DataPlaneSettings.DatabaseName);
        await _dataPlaneDb.CreateContainerIfNotExistsAsync(
            ContainerNames.EffectiveAccess,
            "/principalId");
    }

    public async Task DisposeAsync()
    {
        if (_controlPlaneDb != null)
        {
            await _controlPlaneDb.DeleteAsync();
        }

        if (_dataPlaneDb != null)
        {
            await _dataPlaneDb.DeleteAsync();
        }

        _client?.Dispose();
    }
}
