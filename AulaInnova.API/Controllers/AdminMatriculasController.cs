using AulaInnova.API.DTOs.Admin.Matricula;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/admin/matriculas")]
[Authorize(Roles = "Administrador")]
public class AdminMatriculasController : ControllerBase
{
    private readonly AdminMatriculaService _service;

    public AdminMatriculasController(AdminMatriculaService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<MatriculaResponse>> Create(
        CreateMatriculaRequest request)
    {
        try
        {
            return Ok(await _service.CreateAsync(request, User));
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
    public async Task<ActionResult<List<MatriculaResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<MatriculaResponse>> GetById(
        long id)
    {
        var matricula = await _service.GetByIdAsync(id);

        if (matricula is null)
            return NotFound(new
            {
                mensaje = "Matrícula no encontrada."
            });

        return Ok(matricula);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<MatriculaResponse>> Update(
        long id,
        UpdateMatriculaRequest request)
    {
        try
        {
            return Ok(await _service.UpdateAsync(id, request, User));
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
        [FromQuery] bool anulada)
    {
        try
        {
            await _service.SetStatusAsync(id, anulada, User);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }
}