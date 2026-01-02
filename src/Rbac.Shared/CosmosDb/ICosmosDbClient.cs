using Microsoft.Azure.Cosmos;

namespace Rbac.Shared.CosmosDb;

/// <summary>
/// Abstraction for CosmosDB operations to enable testability.
/// </summary>
public interface ICosmosDbClient
{
    /// <summary>
    /// Gets a container reference.
    /// </summary>
    Container GetContainer(string containerName);

    /// <summary>
    /// Creates a database if it doesn't exist.
    /// </summary>
    Task<Database> CreateDatabaseIfNotExistsAsync(string databaseName);

    /// <summary>
    /// Creates a container if it doesn't exist.
    /// </summary>
    Task<Container> CreateContainerIfNotExistsAsync(
        string databaseName,
        string containerName,
        string partitionKeyPath,
        int? throughput = null);
}

/// <summary>
/// Settings for CosmosDB connection.
/// </summary>
public class CosmosDbSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "RbacDb";
}

/// <summary>
/// Container names for the RBAC system.
/// </summary>
public static class ContainerNames
{
    // Control Plane containers
    public const string RoleDefinitions = "RoleDefinitions";
    public const string RoleAssignments = "RoleAssignments";
    public const string Scopes = "Scopes";
    public const string SyncLeases = "SyncLeases";

    // Data Plane containers
    public const string EffectiveAccess = "EffectiveAccess";
    public const string DataPlaneRoleDefinitions = "RoleDefinitions";
    public const string SyncState = "SyncState";
}
