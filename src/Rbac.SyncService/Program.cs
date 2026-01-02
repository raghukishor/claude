using Rbac.Shared.CosmosDb;
using Rbac.SyncService.Idempotency;
using Rbac.SyncService.Processors;
using Rbac.SyncService.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Configure CosmosDB settings for both control plane and data plane
var controlPlaneSettings = builder.Configuration.GetSection("CosmosDb:ControlPlane").Get<CosmosDbSettings>()
    ?? new CosmosDbSettings
    {
        Endpoint = "https://localhost:8081",
        Key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
        DatabaseName = "RbacControlPlane"
    };

var dataPlaneSettings = builder.Configuration.GetSection("CosmosDb:DataPlane").Get<CosmosDbSettings>()
    ?? new CosmosDbSettings
    {
        Endpoint = "https://localhost:8081",
        Key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
        DatabaseName = "RbacDataPlane"
    };

// Register named CosmosDB clients
builder.Services.AddSingleton(sp => new ControlPlaneCosmosDbClient(controlPlaneSettings));
builder.Services.AddSingleton(sp => new DataPlaneCosmosDbClient(dataPlaneSettings));

// Register helper services for DI
builder.Services.AddSingleton<ICosmosDbClient>(sp => sp.GetRequiredService<ControlPlaneCosmosDbClient>());

// Register processors and engine
builder.Services.AddSingleton<DenormalizationEngine>();
builder.Services.AddSingleton<ReplayDetector>();

builder.Services.AddSingleton<RoleAssignmentProcessor>(sp =>
    new RoleAssignmentProcessor(
        sp.GetRequiredService<DataPlaneCosmosDbClient>(),
        sp.GetRequiredService<ControlPlaneCosmosDbClient>(),
        sp.GetRequiredService<DenormalizationEngine>(),
        sp.GetRequiredService<ReplayDetector>(),
        sp.GetRequiredService<ILogger<RoleAssignmentProcessor>>()));

builder.Services.AddSingleton<RoleDefinitionProcessor>(sp =>
    new RoleDefinitionProcessor(
        sp.GetRequiredService<DataPlaneCosmosDbClient>(),
        sp.GetRequiredService<DenormalizationEngine>(),
        sp.GetRequiredService<ReplayDetector>(),
        sp.GetRequiredService<ILogger<RoleDefinitionProcessor>>()));

// Register background workers
builder.Services.AddHostedService<RoleAssignmentChangeFeedWorker>(sp =>
    new RoleAssignmentChangeFeedWorker(
        sp.GetRequiredService<ControlPlaneCosmosDbClient>(),
        sp.GetRequiredService<RoleAssignmentProcessor>(),
        sp.GetRequiredService<ILogger<RoleAssignmentChangeFeedWorker>>()));

builder.Services.AddHostedService<RoleDefinitionChangeFeedWorker>(sp =>
    new RoleDefinitionChangeFeedWorker(
        sp.GetRequiredService<ControlPlaneCosmosDbClient>(),
        sp.GetRequiredService<RoleDefinitionProcessor>(),
        sp.GetRequiredService<ILogger<RoleDefinitionChangeFeedWorker>>()));

var host = builder.Build();
host.Run();

// Wrapper classes for named CosmosDB clients
public class ControlPlaneCosmosDbClient : CosmosDbClient
{
    public ControlPlaneCosmosDbClient(CosmosDbSettings settings) : base(settings) { }
}

public class DataPlaneCosmosDbClient : CosmosDbClient
{
    public DataPlaneCosmosDbClient(CosmosDbSettings settings) : base(settings) { }
}
