using AulaInnova.API.DTOs.Auth;
using AulaInnova.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AulaInnova.API.Services;

public class AuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByNameAsync(request.Usuario);

        if (user is null)
            return null;

        // CheckPasswordSignInAsync (a diferencia de CheckPasswordAsync)
        // sí incrementa AccessFailedCount y aplica el bloqueo por
        // fuerza bruta configurado en IdentityOptions.Lockout.
        var signInResult = await _signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (!signInResult.Succeeded)
            return null;

        var jwtSettings = _configuration
            .GetSection("Jwt")
            .Get<Configuration.JwtSettings>()
            ?? throw new InvalidOperationException(
                "No se encontró la configuración 'Jwt'.");

        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        claims.AddRange(
            roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Secret));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddMinutes(
            jwtSettings.ExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: jwtSettings.Issuer,
            audience: jwtSettings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new LoginResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiraUtc = expires
        };
    }
}
