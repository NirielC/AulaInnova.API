using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin.Jornada;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminJornadaService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    public AdminJornadaService(
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<JornadaResponse> CreateAsync(
        CreateJornadaRequest request,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.ZonaHoraria))
            throw new ArgumentException("La zona horaria es obligatoria.");

        if (request.FinUtc <= request.InicioUtc)
            throw new ArgumentException(
                "FinUtc debe ser posterior a InicioUtc.");

        if (request.LimitePuntualUtc < request.InicioUtc ||
            request.LimitePuntualUtc > request.FinUtc)
        {
            throw new ArgumentException(
                "LimitePuntualUtc debe estar dentro del intervalo de la jornada.");
        }

        var exists = await _db.Jornadas
            .AnyAsync(j => j.FechaLocal == request.FechaLocal);

        if (exists)
            throw new InvalidOperationException(
                "Ya existe una jornada para esa fecha.");

        var jornada = new Jornada
        {
            FechaLocal = request.FechaLocal,
            ZonaHoraria = request.ZonaHoraria.Trim(),
            InicioUtc = request.InicioUtc,
            FinUtc = request.FinUtc,
            LimitePuntualUtc = request.LimitePuntualUtc,
            Estado = "PROGRAMADA"
        };

        _db.Jornadas.Add(jornada);

        _auditService.Registrar(
            user,
            "Jornada",
            "NUEVA",
            "CREAR",
            "Creación de jornada.",
            antes: null,
            despues: new
            {
                jornada.FechaLocal,
                jornada.ZonaHoraria,
                jornada.InicioUtc,
                jornada.FinUtc,
                jornada.LimitePuntualUtc,
                jornada.Estado
            });

        await _db.SaveChangesAsync();

        return ToResponse(jornada);
    }

    public async Task<List<JornadaResponse>> GetAllAsync()
    {
        return await _db.Jornadas
            .AsNoTracking()
            .OrderByDescending(j => j.FechaLocal)
            .Select(j => new JornadaResponse
            {
                Id = j.Id,
                FechaLocal = j.FechaLocal,
                ZonaHoraria = j.ZonaHoraria,
                InicioUtc = j.InicioUtc,
                FinUtc = j.FinUtc,
                LimitePuntualUtc = j.LimitePuntualUtc,
                Estado = j.Estado
            })
            .ToListAsync();
    }

    public async Task<JornadaResponse?> GetByIdAsync(long id)
    {
        var jornada = await _db.Jornadas
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id);

        return jornada is null
            ? null
            : ToResponse(jornada);
    }

    public async Task<JornadaResponse> UpdateAsync(
        long id,
        UpdateJornadaRequest request,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.ZonaHoraria))
            throw new ArgumentException(
                "La zona horaria es obligatoria.");

        if (request.FinUtc <= request.InicioUtc)
            throw new ArgumentException(
                "FinUtc debe ser posterior a InicioUtc.");

        if (request.LimitePuntualUtc < request.InicioUtc ||
            request.LimitePuntualUtc > request.FinUtc)
        {
            throw new ArgumentException(
                "LimitePuntualUtc debe estar dentro del intervalo de la jornada.");
        }

        var jornada = await _db.Jornadas
            .FirstOrDefaultAsync(j => j.Id == id);

        if (jornada is null)
            throw new KeyNotFoundException(
                "Jornada no encontrada.");

        var antes = new
        {
            jornada.Id,
            jornada.FechaLocal,
            jornada.ZonaHoraria,
            jornada.InicioUtc,
            jornada.FinUtc,
            jornada.LimitePuntualUtc,
            jornada.Estado
        };

        jornada.ZonaHoraria = request.ZonaHoraria.Trim();
        jornada.InicioUtc = request.InicioUtc;
        jornada.FinUtc = request.FinUtc;
        jornada.LimitePuntualUtc = request.LimitePuntualUtc;

        _auditService.Registrar(
            user,
            "Jornada",
            jornada.Id.ToString(),
            "ACTUALIZAR",
            "Actualización de jornada.",
            antes: antes,
            despues: new
            {
                jornada.Id,
                jornada.FechaLocal,
                jornada.ZonaHoraria,
                jornada.InicioUtc,
                jornada.FinUtc,
                jornada.LimitePuntualUtc,
                jornada.Estado
            });

        await _db.SaveChangesAsync();

        return ToResponse(jornada);
    }

    public async Task SetStatusAsync(
        long id,
        string estado,
        ClaimsPrincipal user)
    {
        var estadosPermitidos = new[]
        {
            "PROGRAMADA",
            "ACTIVA",
            "CERRADA",
            "CANCELADA"
        };

        if (!estadosPermitidos.Contains(estado))
            throw new ArgumentException(
                "Estado de jornada no válido.");

        var jornada = await _db.Jornadas
            .FirstOrDefaultAsync(j => j.Id == id);

        if (jornada is null)
            throw new KeyNotFoundException(
                "Jornada no encontrada.");

        var estadoAnterior = jornada.Estado;

        if (estadoAnterior == estado)
            throw new InvalidOperationException(
                "La jornada ya tiene ese estado.");

        jornada.Estado = estado;

        _auditService.Registrar(
            user,
            "Jornada",
            jornada.Id.ToString(),
            "CAMBIAR_ESTADO",
            $"Cambio de estado de jornada de {estadoAnterior} a {estado}.",
            antes: new
            {
                jornada.Id,
                Estado = estadoAnterior
            },
            despues: new
            {
                jornada.Id,
                jornada.Estado
            });

        await _db.SaveChangesAsync();
    }

    private static JornadaResponse ToResponse(Jornada jornada)
    {
        return new JornadaResponse
        {
            Id = jornada.Id,
            FechaLocal = jornada.FechaLocal,
            ZonaHoraria = jornada.ZonaHoraria,
            InicioUtc = jornada.InicioUtc,
            FinUtc = jornada.FinUtc,
            LimitePuntualUtc = jornada.LimitePuntualUtc,
            Estado = jornada.Estado
        };
    }
}