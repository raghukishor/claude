using Rbac.Shared.CosmosDb;
using Rbac.Shared.Models.Common;
using Rbac.Shared.Models.ControlPlane;

namespace Rbac.ControlPlane.Repositories;

public class RoleAssignmentRepository : IRoleAssignmentRepository
{
    private readonly CosmosDbRepository<RoleAssignment> _repository;

    public RoleAssignmentRepository(ICosmosDbClient client)
    {
        _repository = new CosmosDbRepository<RoleAssignment>(
            client,
            ContainerNames.RoleAssignments);
    }

    public async Task<RoleAssignment?> GetByIdAsync(string id, string scope)
    {
        var result = await _repository.GetByIdAsync(id, scope);
        if (result?.Lifecycle?.IsDeleted == true)
            return null;
        return result;
    }

    public async Task<List<RoleAssignment>> ListAsync(string scope, bool includeDeleted = false)
    {
        var query = "SELECT * FROM c WHERE c.scope = @scope";
        var parameters = new Dictionary<string, object>
        {
            { "scope", scope }
        };

        if (!includeDeleted)
        {
            query += " AND (c._lifecycle.isDeleted = false OR NOT IS_DEFINED(c._lifecycle.isDeleted))";
        }

        return await _repository.QueryInPartitionAsync(query, scope, parameters);
    }

    public async Task<List<RoleAssignment>> ListByPrincipalAsync(string principalId, bool includeDeleted = false)
    {
        var query = "SELECT * FROM c WHERE c.principalId = @principalId";
        var parameters = new Dictionary<string, object>
        {
            { "principalId", principalId }
        };

        if (!includeDeleted)
        {
            query += " AND (c._lifecycle.isDeleted = false OR NOT IS_DEFINED(c._lifecycle.isDeleted))";
        }

        return await _repository.QueryAsync(query, parameters);
    }

    public async Task<RoleAssignment> CreateAsync(RoleAssignment roleAssignment)
    {
        return await _repository.CreateAsync(roleAssignment, roleAssignment.Scope);
    }

    public async Task<RoleAssignment> UpdateAsync(RoleAssignment roleAssignment, string? etag = null)
    {
        return await _repository.UpdateAsync(
            roleAssignment,
            roleAssignment.Id,
            roleAssignment.Scope,
            etag);
    }

    public async Task DeleteAsync(string id, string scope)
    {
        var existing = await _repository.GetByIdAsync(id, scope);
        if (existing == null)
            throw new KeyNotFoundException($"Role assignment {id} not found");

        // Soft delete
        existing.Lifecycle ??= new LifecycleInfo();
        existing.Lifecycle.IsDeleted = true;
        existing.Lifecycle.State = "Deleted";
        existing.Lifecycle.Ttl = 86400; // 24 hours

        await _repository.UpdateAsync(existing, id, scope);
    }
}
