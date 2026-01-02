using Rbac.DataPlane.Repositories;
using Rbac.DataPlane.Services;
using Rbac.Shared.CosmosDb;

var builder = WebApplication.CreateBuilder(args);

// Add CosmosDB configuration
var cosmosDbSettings = builder.Configuration.GetSection("CosmosDb").Get<CosmosDbSettings>()
    ?? new CosmosDbSettings
    {
        Endpoint = "https://localhost:8081",
        Key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
        DatabaseName = "RbacDataPlane"
    };

builder.Services.AddSingleton(cosmosDbSettings);

// Register CosmosDB client
builder.Services.AddSingleton<ICosmosDbClient, CosmosDbClient>();

// Register repositories
builder.Services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
builder.Services.AddScoped<IRoleDefinitionRepository, RoleDefinitionRepository>();
builder.Services.AddSingleton<IRoleDefinitionCache, RoleDefinitionCache>();

// Register services
builder.Services.AddSingleton<PermissionEvaluator>();
builder.Services.AddSingleton<ConditionEvaluator>();
builder.Services.AddScoped<ICheckAccessService, CheckAccessService>();

// Add controllers with JSON serialization settings
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
    });

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make the Program class accessible for integration tests
public partial class Program { }
