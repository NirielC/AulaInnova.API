using AulaInnova.API.DTOs.Admin;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/admin/classrooms")]
[Authorize(Roles = "Administrador")]
public class AdminClassroomsController : ControllerBase
{
    private readonly AdminClassroomService _service;

    public AdminClassroomsController(
        AdminClassroomService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<ClassroomResponse>> Create(
        CreateClassroomRequest request)
    {
        try
        {
            return Ok(
                await _service.CreateAsync(
                    request,
                    User));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                mensaje = ex.Message
            });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<ClassroomResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ClassroomResponse>> GetById(
        long id)
    {
        try
        {
            return Ok(await _service.GetByIdAsync(id));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                mensaje = ex.Message
            });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ClassroomResponse>> Update(
        long id,
        UpdateClassroomRequest request)
    {
        try
        {
            return Ok(
                await _service.UpdateAsync(
                    id,
                    request,
                    User));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new
            {
                mensaje = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                mensaje = ex.Message
            });
        }
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
            return NotFound(new
            {
                mensaje = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new
            {
                mensaje = ex.Message
            });
        }
    }
}