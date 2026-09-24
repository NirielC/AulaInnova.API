using AulaInnova.API.DTOs.Admin;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/admin/people")]
[Authorize(Roles = "Administrador")]
public class AdminPeopleController : ControllerBase
{
    private readonly AdminPersonService _service;

    public AdminPeopleController(AdminPersonService service)
    {
        _service = service;
    }

    [HttpPost("students")]
    public async Task<ActionResult<PersonResponse>> CreateStudent(
        CreateStudentRequest request)
    {
        try
        {
            return Ok(
                await _service.CreateStudentAsync(
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

    [HttpPost("staff")]
    public async Task<ActionResult<PersonResponse>> CreateStaff(
        CreateStaffRequest request)
    {
        try
        {
            return Ok(
                await _service.CreateStaffAsync(
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
    public async Task<ActionResult<List<PersonResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<PersonResponse>> GetById(
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
    public async Task<ActionResult<PersonResponse>> Update(
        long id,
        UpdatePersonRequest request)
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