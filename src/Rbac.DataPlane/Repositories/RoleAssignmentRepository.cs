using Rbac.Shared.CosmosDb;
using Rbac.Shared.Models.ControlPlane;

namespace Rbac.DataPlane.Repositories;

public class RoleAssignmentRepository : IRoleAssignmentRepository
{
    private readonly CosmosDbRepository<RoleAssignment> _repository;

    public RoleAssignmentRepository(ICosmosDbClient client)
    {
        _repository = new CosmosDbRepository<RoleAssignment>(
            client,
            ContainerNames.RoleAssignments);
    }

    public async Task<List<RoleAssignment>> ListByPrincipalAsync(string principalId)
    {
        var query = "SELECT * FROM c WHERE c.principalId = @principalId AND (c._lifecycle.isDeleted = false OR NOT IS_DEFINED(c._lifecycle.isDeleted))";
        var parameters = new Dictionary<string, object>
        {
            { "principalId", principalId }
        };

        return await _repository.QueryAsync(query, parameters);
    }
}
