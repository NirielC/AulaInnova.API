using AulaInnova.API.DTOs.Movements;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/[controller]")]
// Ambas rutas las llama la estación NFC de movimientos, no una persona.
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
        var response =
            await _movementService.RegisterExitAsync(request);

        if (response is null)
        {
            return BadRequest(new
            {
                mensaje = "No se pudo registrar la salida."
            });
        }

        return Ok(response);
    }

    [HttpPost("return")]
    public async Task<ActionResult<MovementResponse>> RegisterReturn(
        MovementReturnRequest request)
    {
        var response =
            await _movementService.RegisterReturnAsync(request);

        if (response is null)
        {
            return BadRequest(new
            {
                mensaje = "No se pudo registrar el regreso."
            });
        }

        return Ok(response);
    }
}
