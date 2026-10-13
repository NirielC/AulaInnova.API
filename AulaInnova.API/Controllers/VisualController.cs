using AulaInnova.API.Authentication;
using AulaInnova.API.DTOs.Visual;
using AulaInnova.API.Services;
using AulaInnova.API.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VisualController : ControllerBase
{
    private readonly VisualService _visualService;

    public VisualController(VisualService visualService)
    {
        _visualService = visualService;
    }

    // Operaciones humanas: requieren un usuario de Identity con el permiso
    // correspondiente (CONTEXTO_EDITAR / VISION_CONFIGURAR / VISION_LEER).
    [Authorize]
    [HttpPost("context")]
    public async Task<IActionResult> CreateContext(
        CreateClassContextRequest request)
    {
        try
        {
            var result =
                await _visualService.CreateContextAsync(
                    request,
                    User);

            if (result is null)
                return BadRequest();

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [Authorize]
    [HttpPost("zones")]
    public async Task<IActionResult> CreateZone(
        CreateVisualZoneRequest request)
    {
        try
        {
            var result =
                await _visualService.CreateZoneAsync(
                    request,
                    User);

            if (result is null)
                return BadRequest();

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ConcurrentConflictException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("indicators")]
    public async Task<IActionResult> CreateIndicator(
        CreateVisualIndicatorRequest request)
    {
        try
        {
            var result =
                await _visualService.CreateIndicatorAsync(
                    request,
                    User);

            if (result is null)
                return BadRequest();

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [Authorize]
    [HttpPost("sessions")]
    public async Task<IActionResult> OpenSession(
        OpenVisualSessionRequest request)
    {
        try
        {
            var result =
                await _visualService.OpenSessionAsync(
                    request,
                    User);

            if (result is null)
                return BadRequest();

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ConcurrentConflictException ex)
        {
            return Conflict(new { mensaje = ex.Message });
        }
    }

    // Operación de máquina: solo una estación autenticada con su propio
    // secreto (ai.estacion_secreto) puede reportar mediciones. Ningún
    // usuario de Identity, por más rol que tenga, puede llamar esta ruta.
    [Authorize(AuthenticationSchemes = "Estacion")]
    [HttpPost("measurements")]
    public async Task<IActionResult> RecordMeasurement(
        RecordVisualMeasurementRequest request)
    {
        try
        {
            var result =
                await _visualService.RecordMeasurementAsync(
                    request,
                    User.EstacionId());

            if (result is null)
                return BadRequest();

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            // 403 (no 400): la cámara conserva la medición en su cola
            // hasta que se corrija la configuración de la estación.
            return StatusCode(403, new { mensaje = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("sessions/{sessionId}/close")]
    public async Task<IActionResult> CloseSession(
        long sessionId)
    {
        try
        {
            var result =
                await _visualService.CloseSessionAsync(
                    sessionId,
                    User);

            if (result is null)
                return NotFound();

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // Lectura humana de indicadores agregados (permiso VISION_LEER),
    // ausente hasta ahora: no había ninguna ruta que consultara
    // ai.medicion_visual.
    [Authorize]
    [HttpGet("sessions/{sessionId}/measurements")]
    public async Task<IActionResult> GetMeasurements(
        long sessionId,
        [FromQuery] long? zonaId,
        [FromQuery] long? indicadorId)
    {
        try
        {
            var result = await _visualService.GetMeasurementsAsync(
                sessionId,
                zonaId,
                indicadorId,
                User);

            if (result is null)
                return NotFound();

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
