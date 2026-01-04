using Microsoft.AspNetCore.Mvc;
using Rbac.DataPlane.Services;
using Rbac.Shared.Models.Requests;

namespace Rbac.DataPlane.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CheckAccessController : ControllerBase
{
    private readonly ICheckAccessService _service;

    public CheckAccessController(ICheckAccessService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> CheckAccess([FromBody] CheckAccessRequest request)
    {
        try
        {
            var result = await _service.CheckAccessAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("batch")]
    public async Task<IActionResult> BatchCheckAccess([FromBody] BatchCheckAccessRequest request)
    {
        try
        {
            var result = await _service.BatchCheckAccessAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
