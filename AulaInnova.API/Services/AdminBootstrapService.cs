using AulaInnova.API.Configuration;
using AulaInnova.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AulaInnova.API.Services;

public class AdminBootstrapService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly BootstrapAdminSettings _settings;

    private static readonly string[] OfficialRoles =
    {
        "Administrador",
        "Profesor",
        "Auxiliar",
        "Estudiante"
    };

    public AdminBootstrapService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<BootstrapAdminSettings> settings)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _settings = settings.Value;
    }

    public async Task EnsureAdminAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.Username) ||
            string.IsNullOrWhiteSpace(_settings.Email) ||
            string.IsNullOrWhiteSpace(_settings.Password))
        {
            throw new InvalidOperationException(
                "La configuración de BootstrapAdmin está incompleta.");
        }

        await EnsureOfficialRolesAsync();

        var user = await _userManager.FindByNameAsync(
            _settings.Username);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = _settings.Username,
                Email = _settings.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(
                user,
                _settings.Password);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(e => e.Description));

                throw new InvalidOperationException(
                    $"No se pudo crear el administrador: {errors}");
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        if (currentRoles.Count > 1)
        {
            throw new InvalidOperationException(
                "El usuario administrador tiene más de un rol asignado.");
        }

        if (!currentRoles.Contains("Administrador"))
        {
            if (currentRoles.Count == 1)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(
                    user,
                    currentRoles);

                if (!removeResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "No se pudo corregir el rol del administrador.");
                }
            }

            var roleResult = await _userManager.AddToRoleAsync(
                user,
                "Administrador");

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "No se pudo asignar el rol Administrador.");
            }
        }
    }

    private async Task EnsureOfficialRolesAsync()
    {
        foreach (var roleName in OfficialRoles)
        {
            if (await _roleManager.RoleExistsAsync(roleName))
                continue;

            var result = await _roleManager.CreateAsync(
                new IdentityRole<Guid>(roleName));

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(e => e.Description));

                throw new InvalidOperationException(
                    $"No se pudo crear el rol '{roleName}': {errors}");
            }
        }
    }
}