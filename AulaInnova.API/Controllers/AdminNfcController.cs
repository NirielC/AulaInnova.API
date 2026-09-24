using AulaInnova.API.DTOs.Admin.Nfc;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/admin/nfc")]
[Authorize(Roles = "Administrador")]
public class AdminNfcController : ControllerBase
{
    private readonly AdminNfcService _service;

    public AdminNfcController(AdminNfcService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<NfcCredentialResponse>> Create(
        CreateNfcCredentialRequest request)
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
    public async Task<ActionResult<List<NfcCredentialResponse>>> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<NfcCredentialResponse>> GetById(
        long id)
    {
        var credential = await _service.GetByIdAsync(id);

        if (credential is null)
            return NotFound(new
            {
                mensaje = "Credencial NFC no encontrada."
            });

        return Ok(credential);
    }

    [HttpPost("{id:long}/revoke")]
    public async Task<IActionResult> Revoke(
        long id,
        RevokeNfcCredentialRequest request)
    {
        try
        {
            await _service.RevokeAsync(id, request, User);
            return NoContent();
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