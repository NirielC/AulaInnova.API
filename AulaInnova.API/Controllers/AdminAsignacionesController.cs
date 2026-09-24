using AulaInnova.API.DTOs.Admin.Asignacion;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/admin/asignaciones")]
[Authorize(Roles = "Administrador")]
public class AdminAsignacionesController : ControllerBase
{
    private readonly AdminAsignacionService _service;

    public AdminAsignacionesController(
        AdminAsignacionService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<AsignacionResponse>> Create(
        CreateAsignacionRequest request)
    {
        try
        {
            return Ok(
                await _service.CreateAsync(
                    request,
                    User));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<AsignacionResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AsignacionResponse>> GetById(
        long id)
    {
        var asignacion = await _service.GetByIdAsync(id);

        if (asignacion is null)
            return NotFound(new
            {
                mensaje = "Asignación no encontrada."
            });

        return Ok(asignacion);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AsignacionResponse>> Update(
        long id,
        UpdateAsignacionRequest request)
    {
        try
        {
            return Ok(
                await _service.UpdateAsync(
                    id,
                    request,
                    User));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> SetStatus(
        long id,
        [FromQuery] string estado)
    {
        try
        {
            await _service.SetStatusAsync(
                id,
                estado,
                User);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }
}