using AulaInnova.API.DTOs.Admin.Horario;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/admin/horarios")]
[Authorize(Roles = "Administrador")]
public class AdminHorariosController : ControllerBase
{
    private readonly AdminHorarioService _service;

    public AdminHorariosController(AdminHorarioService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<HorarioResponse>> Create(
        CreateHorarioRequest request)
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
    public async Task<ActionResult<List<HorarioResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<HorarioResponse>> GetById(long id)
    {
        var horario = await _service.GetByIdAsync(id);

        if (horario is null)
            return NotFound(new
            {
                mensaje = "Horario no encontrado."
            });

        return Ok(horario);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<HorarioResponse>> Update(
        long id,
        UpdateHorarioRequest request)
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
        [FromQuery] bool anulado)
    {
        try
        {
            await _service.SetStatusAsync(
                id,
                anulado,
                User);

            return NoContent();
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