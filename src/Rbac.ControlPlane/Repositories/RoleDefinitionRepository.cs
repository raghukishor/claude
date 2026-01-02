using Rbac.Shared.CosmosDb;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.ControlPlane;

namespace Rbac.ControlPlane.Repositories;

public class RoleDefinitionRepository : IRoleDefinitionRepository
{
    private readonly CosmosDbRepository<RoleDefinition> _repository;
    private const string PartitionKey = "RoleDefinition";

    public RoleDefinitionRepository(ICosmosDbClient client)
    {
        _repository = new CosmosDbRepository<RoleDefinition>(
            client,
            ContainerNames.RoleDefinitions);
    }

    public async Task<RoleDefinition?> GetByIdAsync(string id)
    {
        var result = await _repository.GetByIdAsync(id, PartitionKey);
        if (result?.Lifecycle?.IsDeleted == true)
            return null;
        return result;
    }

    public async Task<List<RoleDefinition>> ListAsync(string? scope = null, bool includeDeleted = false)
    {
        var query = "SELECT * FROM c WHERE c.partitionKey = @partitionKey";
        var parameters = new Dictionary<string, object>
        {
            { "partitionKey", PartitionKey }
        };

        if (!includeDeleted)
        {
            query += " AND (c._lifecycle.isDeleted = false OR NOT IS_DEFINED(c._lifecycle.isDeleted))";
        }

        if (!string.IsNullOrEmpty(scope))
        {
            query += " AND ARRAY_CONTAINS(c.assignableScopes, @scope)";
            parameters["scope"] = scope;
        }

        return await _repository.QueryAsync(query, parameters);
    }

    public async Task<RoleDefinition> CreateAsync(RoleDefinition roleDefinition)
    {
        roleDefinition.PartitionKey = PartitionKey;
        return await _repository.CreateAsync(roleDefinition, PartitionKey);
    }

    public async Task<RoleDefinition> UpdateAsync(RoleDefinition roleDefinition, string? etag = null)
    {
        return await _repository.UpdateAsync(roleDefinition, roleDefinition.Id, PartitionKey, etag);
    }

    public async Task DeleteAsync(string id)
    {
        var existing = await _repository.GetByIdAsync(id, PartitionKey);
        if (existing == null)
            throw new KeyNotFoundException($"Role definition {id} not found");

        // Soft delete
        existing.Lifecycle ??= new LifecycleInfo();
        existing.Lifecycle.IsDeleted = true;
        existing.Lifecycle.State = "Deleted";
        existing.Lifecycle.Ttl = 86400; // 24 hours

        await _repository.UpdateAsync(existing, id, PartitionKey);
    }
}
