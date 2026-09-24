using AulaInnova.API.DTOs.Auth;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);

        if (response is null)
        {
            return Unauthorized(new
            {
                mensaje = "Usuario o contraseña incorrectos."
            });
        }

        return Ok(response);
    }
}
