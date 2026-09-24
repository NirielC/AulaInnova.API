using AulaInnova.API.Data;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AuthorizationService
{
    private readonly AulaInnovaDbContext _context;

    public AuthorizationService(AulaInnovaDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string permisoCodigo)
    {
        var userId = GetUserId(user);

        if (userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(permisoCodigo))
        {
            return false;
        }

        var ahora = DateTime.UtcNow;

        return await _context.Concesiones
            .AnyAsync(c =>
                c.UsuarioId == userId &&
                c.PermisoCodigo == permisoCodigo &&
                c.Habilitada &&
                c.DesdeUtc <= ahora &&
                (c.HastaUtc == null || c.HastaUtc >= ahora));
    }

    public async Task<bool> CanAccessGroupAsync(
        ClaimsPrincipal user,
        string permisoCodigo,
        long grupoId)
    {
        var userId = GetUserId(user);

        if (userId == Guid.Empty ||
            grupoId <= 0 ||
            string.IsNullOrWhiteSpace(permisoCodigo))
        {
            return false;
        }

        var ahora = DateTime.UtcNow;

        var concesiones = await _context.Concesiones
            .Where(c =>
                c.UsuarioId == userId &&
                c.PermisoCodigo == permisoCodigo &&
                c.Habilitada &&
                c.DesdeUtc <= ahora &&
                (c.HastaUtc == null || c.HastaUtc >= ahora))
            .ToListAsync();

        foreach (var concesion in concesiones)
        {
            if (concesion.Ambito == "INSTITUCION")
            {
                return true;
            }

            if (concesion.Ambito == "GRUPO" &&
                concesion.GrupoId == grupoId)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> CanAccessAssignmentAsync(
        ClaimsPrincipal user,
        string permisoCodigo,
        long asignacionId)
    {
        var asignacion = await _context.Asignaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.Id == asignacionId);

        if (asignacion is null)
        {
            return false;
        }

        if (await CanAccessGroupAsync(
            user,
            permisoCodigo,
            asignacion.GrupoId))
        {
            return true;
        }

        var userId = GetUserId(user);

        if (userId == Guid.Empty)
        {
            return false;
        }

        var personaId = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.PersonaId)
            .FirstOrDefaultAsync();

        if (!personaId.HasValue)
        {
            return false;
        }

        var personalId = await _context.Personal
            .AsNoTracking()
            .Where(p => p.PersonaId == personaId.Value)
            .Select(p => (long?)p.PersonaId)
            .FirstOrDefaultAsync();

        if (!personalId.HasValue)
        {
            return false;
        }

        if (asignacion.PersonalId == personalId.Value)
        {
            return await HasOwnScopeAsync(
                user,
                permisoCodigo);
        }

        return false;
    }

    public async Task<bool> CanAccessStudentAsync(
        ClaimsPrincipal user,
        string permisoCodigo,
        long estudianteId)
    {
        var userId = GetUserId(user);

        if (userId == Guid.Empty ||
            estudianteId <= 0 ||
            string.IsNullOrWhiteSpace(permisoCodigo))
        {
            return false;
        }

        if (await HasInstitutionScopeAsync(
            user,
            permisoCodigo))
        {
            return true;
        }

        var personaId = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.PersonaId)
            .FirstOrDefaultAsync();

        if (!personaId.HasValue)
        {
            return false;
        }

        var estudiante = await _context.Estudiantes
            .AsNoTracking()
            .Where(e => e.PersonaId == estudianteId)
            .Select(e => new
            {
                e.PersonaId
            })
            .FirstOrDefaultAsync();

        if (estudiante is null)
        {
            return false;
        }

        if (estudiante.PersonaId == personaId.Value)
        {
            return await HasOwnScopeAsync(
                user,
                permisoCodigo);
        }

        var gruposEstudiante = await _context.Matriculas
            .AsNoTracking()
            .Where(m =>
                m.EstudianteId == estudianteId &&
                !m.Anulada)
            .Select(m => m.GrupoId)
            .ToListAsync();

        foreach (var grupoId in gruposEstudiante)
        {
            if (await CanAccessGroupAsync(
                user,
                permisoCodigo,
                grupoId))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> HasInstitutionScopeAsync(
        ClaimsPrincipal user,
        string permisoCodigo)
    {
        var userId = GetUserId(user);

        if (userId == Guid.Empty)
        {
            return false;
        }

        var ahora = DateTime.UtcNow;

        return await _context.Concesiones
            .AnyAsync(c =>
                c.UsuarioId == userId &&
                c.PermisoCodigo == permisoCodigo &&
                c.Ambito == "INSTITUCION" &&
                c.Habilitada &&
                c.DesdeUtc <= ahora &&
                (c.HastaUtc == null || c.HastaUtc >= ahora));
    }

    private static Guid GetUserId(
        ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(
            claim,
            out var userId)
            ? userId
            : Guid.Empty;
    }

    private async Task<bool> HasOwnScopeAsync(
    ClaimsPrincipal user,
    string permisoCodigo)
    {
        var userId = GetUserId(user);

        if (userId == Guid.Empty)
            return false;

        var ahora = DateTime.UtcNow;

        return await _context.Concesiones
            .AnyAsync(c =>
                c.UsuarioId == userId &&
                c.PermisoCodigo == permisoCodigo &&
                c.Ambito == "PROPIO" &&
                c.Habilitada &&
                c.DesdeUtc <= ahora &&
                (c.HastaUtc == null || c.HastaUtc >= ahora));
    }
}