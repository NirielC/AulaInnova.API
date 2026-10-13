using AulaInnova.API.Authentication;
using AulaInnova.API.DTOs.Attendance;
using AulaInnova.API.Services;
using AulaInnova.API.Services.Exceptions;
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
    // 200 = registrada (o ya lo estaba: yaRegistrada = true).
    // 400 = { codigo, mensaje }: la estación muestra el motivo y NO reintenta.
    [Authorize(AuthenticationSchemes = "Estacion")]
    [HttpPost]
    public async Task<ActionResult<AttendanceResponse>> Register(
        AttendanceRequest request)
    {
        request.EstacionId = User.EstacionId();

        try
        {
            return Ok(await _attendanceService.RegisterAsync(request));
        }
        catch (RechazoEstacionException ex)
        {
            return BadRequest(new { codigo = ex.Codigo, mensaje = ex.Message });
        }
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