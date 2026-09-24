using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin.Asignacion;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminAsignacionService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    public AdminAsignacionService(
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<AsignacionResponse> CreateAsync(
        CreateAsignacionRequest request,
        ClaimsPrincipal user)
    {
        Validate(request);

        var jornada = await _db.Jornadas
            .FirstOrDefaultAsync(j => j.Id == request.JornadaId);

        if (jornada is null)
            throw new ArgumentException("La jornada no existe.");

        var grupo = await _db.Grupos
            .FirstOrDefaultAsync(g => g.Id == request.GrupoId);

        if (grupo is null || !grupo.Habilitado)
            throw new ArgumentException(
                "El grupo no existe o está deshabilitado.");

        var personal = await _db.Personal
            .FirstOrDefaultAsync(p => p.PersonaId == request.PersonalId);

        if (personal is null)
            throw new ArgumentException(
                "El personal docente no existe.");

        var aula = await _db.Aulas
            .FirstOrDefaultAsync(a => a.Id == request.AulaId);

        if (aula is null || !aula.Habilitada)
            throw new ArgumentException(
                "El aula no existe o está deshabilitada.");

        if (request.InicioUtc < jornada.InicioUtc ||
            request.FinUtc > jornada.FinUtc)
        {
            throw new ArgumentException(
                "El intervalo de la asignación debe estar dentro de la jornada.");
        }

        HorarioClase? horario = null;

        if (request.HorarioClaseId.HasValue)
        {
            horario = await _db.HorariosClase
                .FirstOrDefaultAsync(h =>
                    h.Id == request.HorarioClaseId.Value &&
                    !h.Anulado);

            if (horario is null)
                throw new ArgumentException(
                    "El horario indicado no existe o está anulado.");

            if (horario.GrupoId != request.GrupoId ||
                horario.DocenteId != request.PersonalId ||
                horario.AulaId != request.AulaId)
            {
                throw new ArgumentException(
                    "El horario no corresponde al grupo, docente y aula indicados.");
            }
        }

        var conflict = await _db.Asignaciones.AnyAsync(a =>
            a.Estado == "ACTIVA" &&
            (
                a.GrupoId == request.GrupoId ||
                a.PersonalId == request.PersonalId ||
                a.AulaId == request.AulaId
            ) &&
            a.InicioUtc < request.FinUtc &&
            a.FinUtc > request.InicioUtc);

        if (conflict)
            throw new InvalidOperationException(
                "Existe una asignación activa que se cruza con este intervalo.");

        var asignacion = new Asignacion
        {
            JornadaId = request.JornadaId,
            GrupoId = request.GrupoId,
            HorarioClaseId = request.HorarioClaseId,
            PersonalId = request.PersonalId,
            AulaId = request.AulaId,
            Tipo = request.Tipo.Trim().ToUpperInvariant(),
            InicioUtc = request.InicioUtc,
            FinUtc = request.FinUtc,
            Estado = "ACTIVA",
            Actividad = string.IsNullOrWhiteSpace(request.Actividad)
                ? null
                : request.Actividad.Trim(),
            CreadoUtc = DateTime.UtcNow
        };

        _db.Asignaciones.Add(asignacion);

        _auditService.Registrar(
            user,
            "Asignacion",
            "NUEVA",
            "CREAR",
            "Creación de asignación.",
            antes: null,
            despues: new
            {
                asignacion.JornadaId,
                asignacion.GrupoId,
                asignacion.HorarioClaseId,
                asignacion.PersonalId,
                asignacion.AulaId,
                asignacion.Tipo,
                asignacion.InicioUtc,
                asignacion.FinUtc,
                asignacion.Estado,
                asignacion.Actividad
            });

        await _db.SaveChangesAsync();

        return ToResponse(asignacion);
    }

    public async Task<List<AsignacionResponse>> GetAllAsync()
    {
        return await _db.Asignaciones
            .AsNoTracking()
            .OrderByDescending(a => a.InicioUtc)
            .Select(a => new AsignacionResponse
            {
                Id = a.Id,
                JornadaId = a.JornadaId,
                GrupoId = a.GrupoId,
                HorarioClaseId = a.HorarioClaseId,
                PersonalId = a.PersonalId,
                AulaId = a.AulaId,
                Tipo = a.Tipo,
                InicioUtc = a.InicioUtc,
                FinUtc = a.FinUtc,
                Estado = a.Estado,
                SustituyeId = a.SustituyeId,
                Actividad = a.Actividad
            })
            .ToListAsync();
    }

    public async Task<AsignacionResponse?> GetByIdAsync(long id)
    {
        var asignacion = await _db.Asignaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        return asignacion is null
            ? null
            : ToResponse(asignacion);
    }

    public async Task<AsignacionResponse> UpdateAsync(
        long id,
        UpdateAsignacionRequest request,
        ClaimsPrincipal user)
    {
        Validate(request);

        var asignacion = await _db.Asignaciones
            .FirstOrDefaultAsync(a => a.Id == id);

        if (asignacion is null)
            throw new KeyNotFoundException(
                "Asignación no encontrada.");

        if (asignacion.Estado != "ACTIVA")
            throw new InvalidOperationException(
                "Solo se pueden modificar asignaciones activas.");

        var jornada = await _db.Jornadas
            .FirstOrDefaultAsync(j => j.Id == asignacion.JornadaId);

        if (jornada is null)
            throw new InvalidOperationException(
                "La jornada asociada no existe.");

        var grupo = await _db.Grupos
            .FirstOrDefaultAsync(g => g.Id == request.GrupoId);

        if (grupo is null || !grupo.Habilitado)
            throw new ArgumentException(
                "El grupo no existe o está deshabilitado.");

        var personal = await _db.Personal
            .FirstOrDefaultAsync(p => p.PersonaId == request.PersonalId);

        if (personal is null)
            throw new ArgumentException(
                "El personal docente no existe.");

        var aula = await _db.Aulas
            .FirstOrDefaultAsync(a => a.Id == request.AulaId);

        if (aula is null || !aula.Habilitada)
            throw new ArgumentException(
                "El aula no existe o está deshabilitada.");

        if (request.InicioUtc < jornada.InicioUtc ||
            request.FinUtc > jornada.FinUtc)
        {
            throw new ArgumentException(
                "El intervalo debe estar dentro de la jornada.");
        }

        if (request.HorarioClaseId.HasValue)
        {
            var horario = await _db.HorariosClase
                .FirstOrDefaultAsync(h =>
                    h.Id == request.HorarioClaseId.Value &&
                    !h.Anulado);

            if (horario is null)
                throw new ArgumentException(
                    "El horario indicado no existe o está anulado.");

            if (horario.GrupoId != request.GrupoId ||
                horario.DocenteId != request.PersonalId ||
                horario.AulaId != request.AulaId)
            {
                throw new ArgumentException(
                    "El horario no corresponde al grupo, docente y aula indicados.");
            }
        }

        var conflict = await _db.Asignaciones.AnyAsync(a =>
            a.Id != id &&
            a.Estado == "ACTIVA" &&
            (
                a.GrupoId == request.GrupoId ||
                a.PersonalId == request.PersonalId ||
                a.AulaId == request.AulaId
            ) &&
            a.InicioUtc < request.FinUtc &&
            a.FinUtc > request.InicioUtc);

        if (conflict)
            throw new InvalidOperationException(
                "Existe una asignación activa que se cruza con este intervalo.");

        var antes = new
        {
            asignacion.Id,
            asignacion.JornadaId,
            asignacion.GrupoId,
            asignacion.HorarioClaseId,
            asignacion.PersonalId,
            asignacion.AulaId,
            asignacion.Tipo,
            asignacion.InicioUtc,
            asignacion.FinUtc,
            asignacion.Estado,
            asignacion.SustituyeId,
            asignacion.Actividad
        };

        asignacion.GrupoId = request.GrupoId;
        asignacion.HorarioClaseId = request.HorarioClaseId;
        asignacion.PersonalId = request.PersonalId;
        asignacion.AulaId = request.AulaId;
        asignacion.Tipo = request.Tipo.Trim().ToUpperInvariant();
        asignacion.InicioUtc = request.InicioUtc;
        asignacion.FinUtc = request.FinUtc;
        asignacion.Actividad = string.IsNullOrWhiteSpace(request.Actividad)
            ? null
            : request.Actividad.Trim();

        _auditService.Registrar(
            user,
            "Asignacion",
            asignacion.Id.ToString(),
            "ACTUALIZAR",
            "Actualización de asignación.",
            antes: antes,
            despues: new
            {
                asignacion.Id,
                asignacion.JornadaId,
                asignacion.GrupoId,
                asignacion.HorarioClaseId,
                asignacion.PersonalId,
                asignacion.AulaId,
                asignacion.Tipo,
                asignacion.InicioUtc,
                asignacion.FinUtc,
                asignacion.Estado,
                asignacion.SustituyeId,
                asignacion.Actividad
            });

        await _db.SaveChangesAsync();

        return ToResponse(asignacion);
    }

    public async Task SetStatusAsync(
        long id,
        string estado,
        ClaimsPrincipal user)
    {
        var estadosPermitidos = new[]
        {
            "ACTIVA",
            "CANCELADA",
            "FINALIZADA"
        };

        if (!estadosPermitidos.Contains(estado))
            throw new ArgumentException(
                "Estado de asignación no válido.");

        var asignacion = await _db.Asignaciones
            .FirstOrDefaultAsync(a => a.Id == id);

        if (asignacion is null)
            throw new KeyNotFoundException(
                "Asignación no encontrada.");

        var estadoAnterior = asignacion.Estado;

        if (estadoAnterior == estado)
            throw new InvalidOperationException(
                "La asignación ya tiene ese estado.");

        asignacion.Estado = estado;

        _auditService.Registrar(
            user,
            "Asignacion",
            asignacion.Id.ToString(),
            "CAMBIAR_ESTADO",
            $"Cambio de estado de asignación de {estadoAnterior} a {estado}.",
            antes: new
            {
                asignacion.Id,
                Estado = estadoAnterior
            },
            despues: new
            {
                asignacion.Id,
                asignacion.Estado
            });

        await _db.SaveChangesAsync();
    }

    private static void Validate(
        CreateAsignacionRequest request)
    {
        ValidateCommon(
            request.Tipo,
            request.InicioUtc,
            request.FinUtc);
    }

    private static void Validate(
        UpdateAsignacionRequest request)
    {
        ValidateCommon(
            request.Tipo,
            request.InicioUtc,
            request.FinUtc);
    }

    private static void ValidateCommon(
        string tipo,
        DateTime inicioUtc,
        DateTime finUtc)
    {
        if (string.IsNullOrWhiteSpace(tipo))
            throw new ArgumentException(
                "El tipo de asignación es obligatorio.");

        if (inicioUtc.Kind != DateTimeKind.Utc ||
            finUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "InicioUtc y FinUtc deben estar expresados en UTC.");
        }

        if (finUtc <= inicioUtc)
            throw new ArgumentException(
                "FinUtc debe ser posterior a InicioUtc.");
    }

    private static AsignacionResponse ToResponse(
        Asignacion asignacion)
    {
        return new AsignacionResponse
        {
            Id = asignacion.Id,
            JornadaId = asignacion.JornadaId,
            GrupoId = asignacion.GrupoId,
            HorarioClaseId = asignacion.HorarioClaseId,
            PersonalId = asignacion.PersonalId,
            AulaId = asignacion.AulaId,
            Tipo = asignacion.Tipo,
            InicioUtc = asignacion.InicioUtc,
            FinUtc = asignacion.FinUtc,
            Estado = asignacion.Estado,
            SustituyeId = asignacion.SustituyeId,
            Actividad = asignacion.Actividad
        };
    }
}