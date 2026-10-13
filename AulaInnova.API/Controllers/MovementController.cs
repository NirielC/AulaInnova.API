using AulaInnova.API.Authentication;
using AulaInnova.API.DTOs.Movements;
using AulaInnova.API.Services;
using AulaInnova.API.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/[controller]")]
// Ambas rutas las llama la estación NFC de movimientos, no una persona.
// 200 = registrado (repetida = true si era un reenvío de la cola offline).
// 400 = { codigo, mensaje }: la estación muestra el motivo y NO reintenta.
[Authorize(AuthenticationSchemes = "Estacion")]
public class MovementController : ControllerBase
{
    private readonly MovementService _movementService;

    public MovementController(MovementService movementService)
    {
        _movementService = movementService;
    }

    [HttpPost("exit")]
    public async Task<ActionResult<MovementResponse>> RegisterExit(
        MovementExitRequest request)
    {
        request.EstacionId = User.EstacionId();

        try
        {
            return Ok(await _movementService.RegisterExitAsync(request));
        }
        catch (RechazoEstacionException ex)
        {
            return BadRequest(new { codigo = ex.Codigo, mensaje = ex.Message });
        }
    }

    [HttpPost("return")]
    public async Task<ActionResult<MovementResponse>> RegisterReturn(
        MovementReturnRequest request)
    {
        request.EstacionId = User.EstacionId();

        try
        {
            return Ok(await _movementService.RegisterReturnAsync(request));
        }
        catch (RechazoEstacionException ex)
        {
            return BadRequest(new { codigo = ex.Codigo, mensaje = ex.Message });
        }
    }
}
