using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin;
using AulaInnova.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminUserService
{
    private static readonly string[] OfficialRoles =
    {
        "Administrador",
        "Profesor",
        "Auxiliar",
        "Estudiante"
    };

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    public AdminUserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _auditService = auditService;
    }

    public async Task<UserResponse> CreateAsync(
        CreateUserRequest request,
        ClaimsPrincipal user)
    {
        ValidateRole(request.Rol);

        if (string.IsNullOrWhiteSpace(request.Usuario) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidOperationException(
                "Usuario, correo y contraseña son obligatorios.");
        }

        var persona = await _db.Personas
            .FirstOrDefaultAsync(p => p.Id == request.PersonaId);

        if (persona is null)
            throw new InvalidOperationException(
                "La persona indicada no existe.");

        if (!persona.Habilitada)
            throw new InvalidOperationException(
                "La persona indicada está deshabilitada.");

        var existingUser = await _db.Users
            .FirstOrDefaultAsync(u => u.PersonaId == request.PersonaId);

        if (existingUser is not null)
            throw new InvalidOperationException(
                "La persona ya tiene una cuenta de usuario.");

        var applicationUser = new ApplicationUser
        {
            UserName = request.Usuario.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            PersonaId = request.PersonaId
        };

        var result = await _userManager.CreateAsync(
            applicationUser,
            request.Password);

        if (!result.Succeeded)
            throw new InvalidOperationException(
                string.Join("; ",
                    result.Errors.Select(e => e.Description)));

        var roleResult = await _userManager.AddToRoleAsync(
            applicationUser,
            request.Rol);

        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(applicationUser);

            throw new InvalidOperationException(
                "No se pudo asignar el rol al usuario.");
        }

        _auditService.Registrar(
            user,
            "AspNetUser",
            applicationUser.Id.ToString(),
            "CREAR",
            "Creación de cuenta de usuario.",
            antes: null,
            despues: new
            {
                applicationUser.Id,
                applicationUser.UserName,
                applicationUser.Email,
                applicationUser.PersonaId,
                Rol = request.Rol
            });

        await _db.SaveChangesAsync();

        return await BuildResponseAsync(applicationUser);
    }

    public async Task<List<UserResponse>> GetAllAsync()
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .ToListAsync();

        var result = new List<UserResponse>();

        foreach (var user in users)
            result.Add(await BuildResponseAsync(user));

        return result;
    }

    public async Task<UserResponse> GetByIdAsync(Guid id)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
            throw new KeyNotFoundException(
                "El usuario no existe.");

        return await BuildResponseAsync(user);
    }

    public async Task<UserResponse> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        ClaimsPrincipal user)
    {
        ValidateRole(request.Rol);

        var applicationUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == id);

        if (applicationUser is null)
            throw new KeyNotFoundException(
                "El usuario no existe.");

        var persona = await _db.Personas
            .FirstOrDefaultAsync(p => p.Id == request.PersonaId);

        if (persona is null)
            throw new InvalidOperationException(
                "La persona indicada no existe.");

        if (!persona.Habilitada)
            throw new InvalidOperationException(
                "La persona indicada está deshabilitada.");

        if (applicationUser.PersonaId != request.PersonaId)
        {
            var otherUser = await _db.Users
                .AnyAsync(u =>
                    u.PersonaId == request.PersonaId &&
                    u.Id != applicationUser.Id);

            if (otherUser)
                throw new InvalidOperationException(
                    "La persona ya tiene otra cuenta de usuario.");
        }

        var currentRoles =
            await _userManager.GetRolesAsync(applicationUser);

        if (currentRoles.Count > 1)
            throw new InvalidOperationException(
                "El usuario tiene más de un rol asignado.");

        var antes = new
        {
            applicationUser.Id,
            applicationUser.UserName,
            applicationUser.Email,
            applicationUser.PersonaId,
            Rol = currentRoles.FirstOrDefault()
        };

        applicationUser.Email = request.Email.Trim();
        applicationUser.PersonaId = request.PersonaId;

        var updateResult =
            await _userManager.UpdateAsync(applicationUser);

        if (!updateResult.Succeeded)
            throw new InvalidOperationException(
                string.Join("; ",
                    updateResult.Errors.Select(e => e.Description)));

        if (currentRoles.Count == 1 &&
            !string.Equals(
                currentRoles[0],
                request.Rol,
                StringComparison.OrdinalIgnoreCase))
        {
            var removeResult =
                await _userManager.RemoveFromRolesAsync(
                    applicationUser,
                    currentRoles);

            if (!removeResult.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo actualizar el rol actual.");

            var addResult =
                await _userManager.AddToRoleAsync(
                    applicationUser,
                    request.Rol);

            if (!addResult.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo asignar el nuevo rol.");
        }
        else if (currentRoles.Count == 0)
        {
            var addResult =
                await _userManager.AddToRoleAsync(
                    applicationUser,
                    request.Rol);

            if (!addResult.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo asignar el rol.");
        }

        _auditService.Registrar(
            user,
            "AspNetUser",
            applicationUser.Id.ToString(),
            "ACTUALIZAR",
            "Actualización de cuenta de usuario.",
            antes: antes,
            despues: new
            {
                applicationUser.Id,
                applicationUser.UserName,
                applicationUser.Email,
                applicationUser.PersonaId,
                Rol = request.Rol
            });

        await _db.SaveChangesAsync();

        return await BuildResponseAsync(applicationUser);
    }

    public async Task SetActiveAsync(
        Guid id,
        bool active,
        ClaimsPrincipal user)
    {
        var applicationUser = await _userManager.Users
            .FirstOrDefaultAsync(u => u.Id == id);

        if (applicationUser is null)
            throw new KeyNotFoundException(
                "El usuario no existe.");

        var estadoAnterior =
            !applicationUser.LockoutEnd.HasValue ||
            applicationUser.LockoutEnd <= DateTimeOffset.UtcNow;

        if (estadoAnterior == active)
            throw new InvalidOperationException(
                active
                    ? "El usuario ya está activo."
                    : "El usuario ya está inactivo.");

        applicationUser.LockoutEnabled = true;

        applicationUser.LockoutEnd = active
            ? null
            : DateTimeOffset.UtcNow.AddYears(100);

        var result =
            await _userManager.UpdateAsync(applicationUser);

        if (!result.Succeeded)
            throw new InvalidOperationException(
                string.Join("; ",
                    result.Errors.Select(e => e.Description)));

        _auditService.Registrar(
            user,
            "AspNetUser",
            applicationUser.Id.ToString(),
            active ? "ACTIVAR" : "DESACTIVAR",
            active
                ? "Activación de cuenta de usuario."
                : "Desactivación de cuenta de usuario.",
            antes: new
            {
                applicationUser.Id,
                Activo = estadoAnterior
            },
            despues: new
            {
                applicationUser.Id,
                Activo = active
            });

        await _db.SaveChangesAsync();
    }

    private static void ValidateRole(string role)
    {
        if (!OfficialRoles.Contains(
                role,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "El rol indicado no es válido.");
        }
    }

    private async Task<UserResponse> BuildResponseAsync(
        ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Count > 1)
            throw new InvalidOperationException(
                "El usuario tiene más de un rol asignado.");

        return new UserResponse
        {
            Id = user.Id,
            Usuario = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Activo = !user.LockoutEnd.HasValue ||
                     user.LockoutEnd <= DateTimeOffset.UtcNow,
            PersonaId = user.PersonaId,
            Rol = roles.FirstOrDefault()
        };
    }
}