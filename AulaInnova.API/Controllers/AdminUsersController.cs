using AulaInnova.API.DTOs.Admin;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Administrador")]
public class AdminUsersController : ControllerBase
{
    private readonly AdminUserService _service;

    public AdminUsersController(AdminUserService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request)
    {
        try
        {
            var response = await _service.CreateAsync(
                request,
                User);

            return Ok(response);
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
    public async Task<ActionResult<List<UserResponse>>> GetAll()
    {
        var response = await _service.GetAllAsync();

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(
        Guid id)
    {
        try
        {
            var response = await _service.GetByIdAsync(id);

            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                mensaje = ex.Message
            });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(
        Guid id,
        UpdateUserRequest request)
    {
        try
        {
            var response = await _service.UpdateAsync(
                id,
                request,
                User);

            return Ok(response);
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

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(
        Guid id,
        [FromQuery] bool activo)
    {
        try
        {
            await _service.SetActiveAsync(
                id,
                activo,
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