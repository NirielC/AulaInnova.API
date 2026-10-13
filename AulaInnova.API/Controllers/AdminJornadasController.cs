using AulaInnova.API.DTOs.Admin.Jornada;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/admin/jornadas")]
[Authorize(Roles = "Administrador")]
public class AdminJornadasController : ControllerBase
{
    private readonly AdminJornadaService _service;

    public AdminJornadasController(AdminJornadaService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<JornadaResponse>> Create(
        CreateJornadaRequest request)
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
    public async Task<ActionResult<List<JornadaResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<JornadaResponse>> GetById(long id)
    {
        var jornada = await _service.GetByIdAsync(id);

        if (jornada is null)
            return NotFound(new
            {
                mensaje = "Jornada no encontrada."
            });

        return Ok(jornada);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<JornadaResponse>> Update(
        long id,
        UpdateJornadaRequest request)
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

    // Crea las jornadas de un periodo (p. ej. todo el mes, lunes a
    // viernes) y sus clases a partir del horario semanal.
    [HttpPost("rango")]
    public async Task<ActionResult<GeneracionJornadasResponse>> CreateRange(
        CreateJornadasRangoRequest request)
    {
        try
        {
            return Ok(await _service.CreateRangeAsync(request, User));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    // Vuelve a generar las clases (asignaciones) de la jornada desde el
    // horario. Solo agrega las que falten; no toca las existentes.
    [HttpPost("{id:long}/asignaciones")]
    public async Task<ActionResult<ResultadoGeneracion>> GenerateAssignments(
        long id)
    {
        try
        {
            return Ok(await _service.GenerateAssignmentsAsync(id));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }
}
