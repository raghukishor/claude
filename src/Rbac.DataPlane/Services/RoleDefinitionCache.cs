using System.Collections.Concurrent;
using Rbac.DataPlane.Repositories;
using Rbac.Shared.Models.ControlPlane;

namespace Rbac.DataPlane.Services;

public interface IRoleDefinitionCache
{
    Task<RoleDefinition?> GetAsync(string id);
}

public class RoleDefinitionCache : IRoleDefinitionCache
{
    private readonly IRoleDefinitionRepository _repository;
    private readonly ConcurrentDictionary<string, CachedRoleDefinition> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

    public RoleDefinitionCache(IRoleDefinitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<RoleDefinition?> GetAsync(string id)
    {
        if (_cache.TryGetValue(id, out var cached) && !cached.IsExpired(_ttl))
        {
            return cached.RoleDefinition;
        }

        var roleDefinition = await _repository.GetByIdAsync(id);
        
        if (roleDefinition != null)
        {
            _cache[id] = new CachedRoleDefinition(roleDefinition, DateTimeOffset.UtcNow);
        }

        return roleDefinition;
    }

    private class CachedRoleDefinition
    {
        public RoleDefinition RoleDefinition { get; }
        public DateTimeOffset CachedAt { get; }

        public CachedRoleDefinition(RoleDefinition roleDefinition, DateTimeOffset cachedAt)
        {
            RoleDefinition = roleDefinition;
            CachedAt = cachedAt;
        }

        public bool IsExpired(TimeSpan ttl)
        {
            return DateTimeOffset.UtcNow > CachedAt.Add(ttl);
        }
    }
}
