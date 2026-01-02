using Microsoft.AspNetCore.Mvc;
using Rbac.ControlPlane.Services;
using Rbac.Shared.Models.Requests;

namespace Rbac.ControlPlane.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class RoleDefinitionsController : ControllerBase
{
    private readonly IRoleDefinitionService _service;

    public RoleDefinitionsController(IRoleDefinitionService service)
    {
        _service = service;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var result = await _service.GetByIdAsync(id);
        if (result == null)
            return NotFound(new { error = $"Role definition {id} not found" });

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? scope = null)
    {
        var results = await _service.ListAsync(scope);
        return Ok(results);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleDefinitionRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateRoleDefinitionRequest request,
        [FromHeader(Name = "If-Match")] string? etag = null)
    {
        try
        {
            var result = await _service.UpdateAsync(id, request, etag);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Role definition {id} not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Role definition {id} not found" });
        }
    }
}
