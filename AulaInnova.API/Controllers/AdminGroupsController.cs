using AulaInnova.API.DTOs.Admin;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/admin/groups")]
[Authorize(Roles = "Administrador")]
public class AdminGroupsController : ControllerBase
{
    private readonly AdminGroupService _service;

    public AdminGroupsController(AdminGroupService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<GroupResponse>> Create(
        CreateGroupRequest request)
    {
        try
        {
            return Ok(
                await _service.CreateAsync(
                    request,
                    User));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                mensaje = ex.Message
            });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<GroupResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<GroupResponse>> GetById(long id)
    {
        try
        {
            return Ok(await _service.GetByIdAsync(id));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                mensaje = ex.Message
            });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<GroupResponse>> Update(
        long id,
        UpdateGroupRequest request)
    {
        try
        {
            return Ok(
                await _service.UpdateAsync(
                    id,
                    request,
                    User));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                mensaje = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                mensaje = ex.Message
            });
        }
    }

    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> SetStatus(
        long id,
        [FromQuery] bool habilitado)
    {
        try
        {
            await _service.SetStatusAsync(
                id,
                habilitado,
                User);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                mensaje = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                mensaje = ex.Message
            });
        }
    }
}