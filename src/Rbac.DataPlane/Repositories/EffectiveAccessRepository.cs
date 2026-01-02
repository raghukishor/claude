using Rbac.Shared.CosmosDb;
using Rbac.Shared.Models.DataPlane;
using Rbac.Shared.Utilities;

namespace Rbac.DataPlane.Repositories;

public class EffectiveAccessRepository : IEffectiveAccessRepository
{
    private readonly CosmosDbRepository<EffectiveAccess> _repository;

    public EffectiveAccessRepository(ICosmosDbClient client)
    {
        _repository = new CosmosDbRepository<EffectiveAccess>(
            client,
            ContainerNames.EffectiveAccess);
    }

    public async Task<EffectiveAccess?> GetByIdAsync(string id, string partitionKey)
    {
        return await _repository.GetByIdAsync(id, partitionKey);
    }

    public async Task<EffectiveAccess?> GetByPrincipalAndScopeAsync(string principalId, string scope)
    {
        var id = ScopeHasher.GenerateEffectiveAccessId(principalId, scope);
        return await _repository.GetByIdAsync(id, principalId);
    }

    public async Task<List<EffectiveAccess>> GetByPrincipalAsync(string principalId)
    {
        return await _repository.QueryInPartitionAsync(
            "SELECT * FROM c WHERE c.principalId = @principalId",
            principalId,
            new Dictionary<string, object> { { "principalId", principalId } });
    }

    public async Task<EffectiveAccess> UpsertAsync(EffectiveAccess effectiveAccess)
    {
        return await _repository.UpsertAsync(effectiveAccess, effectiveAccess.PrincipalId);
    }

    public async Task DeleteAsync(string id, string partitionKey)
    {
        await _repository.DeleteAsync(id, partitionKey);
    }
}
