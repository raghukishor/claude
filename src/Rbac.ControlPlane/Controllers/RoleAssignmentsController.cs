using Microsoft.AspNetCore.Mvc;
using Rbac.ControlPlane.Services;
using Rbac.Shared.Models.Requests;

namespace Rbac.ControlPlane.Controllers;

[ApiController]
[Route("api/v1/scopes/{scope}/[controller]")]
public class RoleAssignmentsController : ControllerBase
{
    private readonly IRoleAssignmentService _service;

    public RoleAssignmentsController(IRoleAssignmentService service)
    {
        _service = service;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string scope, string id)
    {
        var decodedScope = Uri.UnescapeDataString(scope);
        var result = await _service.GetByIdAsync(id, decodedScope);
        if (result == null)
            return NotFound(new { error = $"Role assignment {id} not found" });

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(string scope)
    {
        var decodedScope = Uri.UnescapeDataString(scope);
        var results = await _service.ListAsync(decodedScope);
        return Ok(results);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string scope, [FromBody] CreateRoleAssignmentRequest request)
    {
        var decodedScope = Uri.UnescapeDataString(scope);
        try
        {
            var result = await _service.CreateAsync(decodedScope, request);
            return CreatedAtAction(
                nameof(GetById),
                new { scope = scope, id = result.Id },
                result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string scope, string id)
    {
        var decodedScope = Uri.UnescapeDataString(scope);
        try
        {
            await _service.DeleteAsync(id, decodedScope);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Role assignment {id} not found" });
        }
    }
}

[ApiController]
[Route("api/v1/principals/{principalId}/roleAssignments")]
public class PrincipalRoleAssignmentsController : ControllerBase
{
    private readonly IRoleAssignmentService _service;

    public PrincipalRoleAssignmentsController(IRoleAssignmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> ListByPrincipal(string principalId)
    {
        var results = await _service.ListByPrincipalAsync(principalId);
        return Ok(results);
    }
}
