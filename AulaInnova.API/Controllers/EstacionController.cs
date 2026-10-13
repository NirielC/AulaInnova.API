using AulaInnova.API.Authentication;
using AulaInnova.API.DTOs.Estacion;
using AulaInnova.API.Services;
using AulaInnova.API.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

/// <summary>
/// Rutas que usan las estaciones físicas (NFC y cámara) con su propio
/// secreto: encabezados X-Estacion-Id y X-Estacion-Clave. El id de la
/// estación sale siempre de esa autenticación, nunca del cuerpo.
///
/// Errores de negocio: 400 { codigo, mensaje }. La estación muestra el
/// mensaje según el código y no reintenta (reenviar lo mismo no lo arregla).
/// </summary>
[ApiController]
[Route("api/estacion")]
[Authorize(AuthenticationSchemes = "Estacion")]
public class EstacionController : ControllerBase
{
    private readonly EstacionService _service;

    public EstacionController(EstacionService service)
    {
        _service = service;
    }

    // Al arrancar: quién soy, en qué aula estoy, qué modos tengo y la hora.
    [HttpGet("configuracion")]
    public Task<IActionResult> Configuracion() =>
        Ejecutar(() => _service.ConfiguracionAsync(User.EstacionId()));

    // Cada pocos minutos: "sigo viva" y el modo que muestro.
    [HttpPost("heartbeat")]
    public Task<IActionResult> Heartbeat(HeartbeatRequest request) =>
        Ejecutar(() => _service.HeartbeatAsync(User.EstacionId(), request));

    // Quién es el dueño de una tarjeta (menú de administración).
    [HttpGet("credencial/{referencia}")]
    public Task<IActionResult> Credencial(string referencia) =>
        Ejecutar(() => _service.CredencialAsync(User.EstacionId(), referencia));

    // Una tarjeta pasada: asistencia, presencia del personal o, en
    // movimientos, si el estudiante debe elegir motivo o confirmar regreso.
    [HttpPost("lectura")]
    public Task<IActionResult> Lectura(LecturaEstacionRequest request) =>
        Ejecutar(() => _service.LecturaAsync(User.EstacionId(), request));

    // Cámara: sesión visual de la clase que hay ahora en su aula.
    [HttpPost("vision/sesion")]
    public Task<IActionResult> SesionVision(SesionVisionRequest request) =>
        Ejecutar(() => _service.SesionVisionAsync(User.EstacionId(), request));

    private async Task<IActionResult> Ejecutar<T>(Func<Task<T>> accion)
    {
        try
        {
            return Ok(await accion());
        }
        catch (RechazoEstacionException ex)
        {
            return BadRequest(new { codigo = ex.Codigo, mensaje = ex.Message });
        }
        catch (ConcurrentConflictException ex)
        {
            return Conflict(new { codigo = "CONFLICTO", mensaje = ex.Message });
        }
    }
}
