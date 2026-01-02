using Rbac.Shared.Models.Requests;
using Rbac.Shared.Models.Responses;

namespace Rbac.DataPlane.Services;

public interface ICheckAccessService
{
    Task<CheckAccessResponse> CheckAccessAsync(CheckAccessRequest request);
    Task<BatchCheckAccessResponse> BatchCheckAccessAsync(BatchCheckAccessRequest request);
}
