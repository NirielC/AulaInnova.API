using AulaInnova.API.DTOs.Admin.Estacion;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/admin/fallas-estacion")]
[Authorize(Roles = "Administrador")]
public class AdminFallasEstacionController : ControllerBase
{
    private readonly AdminFallaEstacionService _service;

    public AdminFallasEstacionController(
        AdminFallaEstacionService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<FallaEstacionResponse>> Create(
        CreateFallaEstacionRequest request)
    {
        try
        {
            return Ok(
                await _service.CreateAsync(
                    request,
                    GetUserId(),
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

    [HttpGet]
    public async Task<ActionResult<List<FallaEstacionResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<FallaEstacionResponse>> GetById(
        long id)
    {
        var falla = await _service.GetByIdAsync(id);

        if (falla is null)
            return NotFound(new
            {
                mensaje = "Falla de estación no encontrada."
            });

        return Ok(falla);
    }

    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> Close(long id)
    {
        try
        {
            await _service.CloseAsync(id, User);
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

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException();

        return userId;
    }
}