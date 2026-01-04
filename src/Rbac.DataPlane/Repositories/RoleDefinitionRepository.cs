using Rbac.Shared.CosmosDb;
using Rbac.Shared.Models.ControlPlane;

namespace Rbac.DataPlane.Repositories;

public class RoleDefinitionRepository : IRoleDefinitionRepository
{
    private readonly CosmosDbRepository<RoleDefinition> _repository;

    public RoleDefinitionRepository(ICosmosDbClient client)
    {
        _repository = new CosmosDbRepository<RoleDefinition>(
            client,
            ContainerNames.RoleDefinitions);
    }

    public async Task<RoleDefinition?> GetByIdAsync(string id)
    {
        // RoleDefinitions are partitioned by "RoleDefinition" (static partition key) or their ID? 
        // Checking RoleDefinition.cs: PartitionKey = "RoleDefinition" by default.
        return await _repository.GetByIdAsync(id, "RoleDefinition");
    }
}
