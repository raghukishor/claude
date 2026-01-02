using Rbac.Shared.Models.DataPlane;

namespace Rbac.DataPlane.Repositories;

public interface IEffectiveAccessRepository
{
    Task<EffectiveAccess?> GetByIdAsync(string id, string partitionKey);
    Task<EffectiveAccess?> GetByPrincipalAndScopeAsync(string principalId, string scope);
    Task<List<EffectiveAccess>> GetByPrincipalAsync(string principalId);
    Task<EffectiveAccess> UpsertAsync(EffectiveAccess effectiveAccess);
    Task DeleteAsync(string id, string partitionKey);
}
