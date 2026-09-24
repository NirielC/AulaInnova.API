using AulaInnova.API.DTOs.Attendance;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly AttendanceService _attendanceService;

    public AttendanceController(AttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    // Lo llama la estación NFC, no una persona: solo su propio secreto
    // (ai.estacion_secreto) autentica esta ruta.
    [Authorize(AuthenticationSchemes = "Estacion")]
    [HttpPost]
    public async Task<ActionResult<AttendanceResponse>> Register(
        AttendanceRequest request)
    {
        var response = await _attendanceService.RegisterAsync(request);

        if (response is null)
        {
            return BadRequest(new
            {
                mensaje = "No se pudo registrar la asistencia."
            });
        }

        return Ok(response);
    }

    // Lo hace un operador humano (docente/administrativo) con permiso;
    // usa el JWT normal de Identity.
    [Authorize]
    [HttpPost("manual")]
    public async Task<ActionResult<ManualAttendanceResponse>> RegisterManual(
        ManualAttendanceRequest request)
    {
        try
        {
            return Ok(
                await _attendanceService.RegisterManualAsync(
                    request,
                    User));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
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