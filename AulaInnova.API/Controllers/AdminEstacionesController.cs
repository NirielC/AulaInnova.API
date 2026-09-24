using AulaInnova.API.DTOs.Admin.Estacion;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/admin/estaciones")]
[Authorize(Roles = "Administrador")]
public class AdminEstacionesController : ControllerBase
{
    private readonly AdminEstacionService _service;

    public AdminEstacionesController(AdminEstacionService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<EstacionResponse>> Create(
        CreateEstacionRequest request)
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
    public async Task<ActionResult<List<EstacionResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<EstacionResponse>> GetById(
        long id)
    {
        var estacion = await _service.GetByIdAsync(id);

        if (estacion is null)
            return NotFound(new
            {
                mensaje = "Estación no encontrada."
            });

        return Ok(estacion);
    }

    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> SetStatus(
        long id,
        [FromQuery] bool habilitada)
    {
        try
        {
            await _service.SetStatusAsync(
                id,
                habilitada,
                User);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpPost("{id:long}/configuration")]
    public async Task<ActionResult<EstacionConfigurationResponse>>
        Configure(
            long id,
            ConfigureEstacionRequest request)
    {
        try
        {
            var userId = GetUserId();

            var result = await _service.ConfigureAsync(
                id,
                request,
                userId,
                User);

            return Ok(result);
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

    [HttpGet("{id:long}/configuration")]
    public async Task<ActionResult<EstacionConfigurationResponse>>
        GetCurrentConfiguration(long id)
    {
        var configuration =
            await _service.GetCurrentConfigurationAsync(id);

        if (configuration is null)
            return NotFound(new
            {
                mensaje = "La estación no tiene configuración vigente."
            });

        return Ok(configuration);
    }

    [HttpPost("{id:long}/mode")]
    public async Task<IActionResult> ConfigureMode(
        long id,
        [FromQuery] string modo)
    {
        try
        {
            await _service.ConfigureModeAsync(
                id,
                modo,
                GetUserId(),
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

    // Emite (y rota) el secreto que la estación física debe usar para
    // autenticarse ante /api/Attendance, /api/Movement y
    // /api/Visual/measurements. El valor en claro solo se muestra aquí;
    // guárdelo de inmediato, no se puede volver a consultar.
    [HttpPost("{id:long}/secret")]
    public async Task<ActionResult<EstacionSecretResponse>> IssueSecret(
        long id)
    {
        try
        {
            return Ok(await _service.IssueSecretAsync(id, User));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    [HttpDelete("{id:long}/secret/{identificadorClave}")]
    public async Task<IActionResult> RevokeSecret(
        long id,
        string identificadorClave)
    {
        try
        {
            await _service.RevokeSecretAsync(
                id,
                identificadorClave,
                User);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException();

        return userId;
    }
}