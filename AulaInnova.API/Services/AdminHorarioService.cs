using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin.Horario;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminHorarioService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    public AdminHorarioService(
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<HorarioResponse> CreateAsync(
        CreateHorarioRequest request,
        ClaimsPrincipal user)
    {
        Validate(request);

        var asignatura = await _db.Asignaturas
            .FirstOrDefaultAsync(a => a.Id == request.AsignaturaId);

        if (asignatura is null || !asignatura.Habilitada)
            throw new ArgumentException(
                "La asignatura no existe o está deshabilitada.");

        var grupo = await _db.Grupos
            .FirstOrDefaultAsync(g => g.Id == request.GrupoId);

        if (grupo is null || !grupo.Habilitado)
            throw new ArgumentException(
                "El grupo no existe o está deshabilitado.");

        var docente = await _db.Personal
            .FirstOrDefaultAsync(p =>
                p.PersonaId == request.DocenteId);

        if (docente is null)
            throw new ArgumentException(
                "El docente no existe.");

        var aula = await _db.Aulas
            .FirstOrDefaultAsync(a => a.Id == request.AulaId);

        if (aula is null || !aula.Habilitada)
            throw new ArgumentException(
                "El aula no existe o está deshabilitada.");

        var conflict = await _db.HorariosClase.AnyAsync(h =>
            !h.Anulado &&
            h.GrupoId == request.GrupoId &&
            h.DiaSemana == request.DiaSemana &&
            h.HoraInicio < request.HoraFin &&
            h.HoraFin > request.HoraInicio &&
            h.VigenteDesde <= request.VigenteDesde &&
            (h.VigenteHasta == null ||
             request.VigenteDesde <= h.VigenteHasta));

        if (conflict)
            throw new InvalidOperationException(
                "El grupo ya tiene un horario que se cruza con este intervalo.");

        var horario = new HorarioClase
        {
            AsignaturaId = request.AsignaturaId,
            GrupoId = request.GrupoId,
            DocenteId = request.DocenteId,
            AulaId = request.AulaId,
            DiaSemana = request.DiaSemana,
            VigenteDesde = request.VigenteDesde,
            VigenteHasta = request.VigenteHasta,
            HoraInicio = request.HoraInicio,
            HoraFin = request.HoraFin,
            Anulado = false,
            CreadoUtc = DateTime.UtcNow
        };

        _db.HorariosClase.Add(horario);

        _auditService.Registrar(
            user,
            "HorarioClase",
            "NUEVO",
            "CREAR",
            "Creación de horario de clase.",
            antes: null,
            despues: new
            {
                horario.AsignaturaId,
                horario.GrupoId,
                horario.DocenteId,
                horario.AulaId,
                horario.DiaSemana,
                horario.VigenteDesde,
                horario.VigenteHasta,
                horario.HoraInicio,
                horario.HoraFin,
                horario.Anulado
            });

        await _db.SaveChangesAsync();

        return ToResponse(horario);
    }

    public async Task<List<HorarioResponse>> GetAllAsync()
    {
        return await _db.HorariosClase
            .AsNoTracking()
            .OrderBy(h => h.DiaSemana)
            .ThenBy(h => h.HoraInicio)
            .Select(h => new HorarioResponse
            {
                Id = h.Id,
                AsignaturaId = h.AsignaturaId,
                GrupoId = h.GrupoId,
                DocenteId = h.DocenteId,
                AulaId = h.AulaId,
                DiaSemana = h.DiaSemana,
                VigenteDesde = h.VigenteDesde,
                VigenteHasta = h.VigenteHasta,
                HoraInicio = h.HoraInicio,
                HoraFin = h.HoraFin,
                Anulado = h.Anulado
            })
            .ToListAsync();
    }

    public async Task<HorarioResponse?> GetByIdAsync(long id)
    {
        var horario = await _db.HorariosClase
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id);

        return horario is null
            ? null
            : ToResponse(horario);
    }

    public async Task<HorarioResponse> UpdateAsync(
        long id,
        UpdateHorarioRequest request,
        ClaimsPrincipal user)
    {
        Validate(request);

        var horario = await _db.HorariosClase
            .FirstOrDefaultAsync(h => h.Id == id);

        if (horario is null)
            throw new KeyNotFoundException(
                "Horario no encontrado.");

        var asignatura = await _db.Asignaturas
            .FirstOrDefaultAsync(a => a.Id == request.AsignaturaId);

        if (asignatura is null || !asignatura.Habilitada)
            throw new ArgumentException(
                "La asignatura no existe o está deshabilitada.");

        var grupo = await _db.Grupos
            .FirstOrDefaultAsync(g => g.Id == request.GrupoId);

        if (grupo is null || !grupo.Habilitado)
            throw new ArgumentException(
                "El grupo no existe o está deshabilitado.");

        var docente = await _db.Personal
            .FirstOrDefaultAsync(p =>
                p.PersonaId == request.DocenteId);

        if (docente is null)
            throw new ArgumentException(
                "El docente no existe.");

        var aula = await _db.Aulas
            .FirstOrDefaultAsync(a => a.Id == request.AulaId);

        if (aula is null || !aula.Habilitada)
            throw new ArgumentException(
                "El aula no existe o está deshabilitada.");

        var conflict = await _db.HorariosClase.AnyAsync(h =>
            h.Id != id &&
            !h.Anulado &&
            h.GrupoId == request.GrupoId &&
            h.DiaSemana == request.DiaSemana &&
            h.HoraInicio < request.HoraFin &&
            h.HoraFin > request.HoraInicio &&
            h.VigenteDesde <= request.VigenteDesde &&
            (h.VigenteHasta == null ||
             request.VigenteDesde <= h.VigenteHasta));

        if (conflict)
            throw new InvalidOperationException(
                "El grupo ya tiene un horario que se cruza con este intervalo.");

        var antes = new
        {
            horario.Id,
            horario.AsignaturaId,
            horario.GrupoId,
            horario.DocenteId,
            horario.AulaId,
            horario.DiaSemana,
            horario.VigenteDesde,
            horario.VigenteHasta,
            horario.HoraInicio,
            horario.HoraFin,
            horario.Anulado
        };

        horario.AsignaturaId = request.AsignaturaId;
        horario.GrupoId = request.GrupoId;
        horario.DocenteId = request.DocenteId;
        horario.AulaId = request.AulaId;
        horario.DiaSemana = request.DiaSemana;
        horario.VigenteDesde = request.VigenteDesde;
        horario.VigenteHasta = request.VigenteHasta;
        horario.HoraInicio = request.HoraInicio;
        horario.HoraFin = request.HoraFin;

        _auditService.Registrar(
            user,
            "HorarioClase",
            horario.Id.ToString(),
            "ACTUALIZAR",
            "Actualización de horario de clase.",
            antes: antes,
            despues: new
            {
                horario.Id,
                horario.AsignaturaId,
                horario.GrupoId,
                horario.DocenteId,
                horario.AulaId,
                horario.DiaSemana,
                horario.VigenteDesde,
                horario.VigenteHasta,
                horario.HoraInicio,
                horario.HoraFin,
                horario.Anulado
            });

        await _db.SaveChangesAsync();

        return ToResponse(horario);
    }

    public async Task SetStatusAsync(
        long id,
        bool anulado,
        ClaimsPrincipal user)
    {
        var horario = await _db.HorariosClase
            .FirstOrDefaultAsync(h => h.Id == id);

        if (horario is null)
            throw new KeyNotFoundException(
                "Horario no encontrado.");

        if (horario.Anulado == anulado)
            throw new InvalidOperationException(
                anulado
                    ? "El horario ya está anulado."
                    : "El horario ya está activo.");

        var estadoAnterior = horario.Anulado;

        horario.Anulado = anulado;

        _auditService.Registrar(
            user,
            "HorarioClase",
            horario.Id.ToString(),
            anulado ? "ANULAR" : "REACTIVAR",
            anulado
                ? "Anulación de horario de clase."
                : "Reactivación de horario de clase.",
            antes: new
            {
                horario.Id,
                Anulado = estadoAnterior
            },
            despues: new
            {
                horario.Id,
                horario.Anulado
            });

        await _db.SaveChangesAsync();
    }

    private static void Validate(
        CreateHorarioRequest request)
    {
        ValidateCommon(
            request.DiaSemana,
            request.VigenteDesde,
            request.VigenteHasta,
            request.HoraInicio,
            request.HoraFin);
    }

    private static void Validate(
        UpdateHorarioRequest request)
    {
        ValidateCommon(
            request.DiaSemana,
            request.VigenteDesde,
            request.VigenteHasta,
            request.HoraInicio,
            request.HoraFin);
    }

    private static void ValidateCommon(
        byte diaSemana,
        DateOnly vigenteDesde,
        DateOnly? vigenteHasta,
        TimeOnly horaInicio,
        TimeOnly horaFin)
    {
        if (diaSemana > 6)
            throw new ArgumentException(
                "DiaSemana debe estar entre 0 y 6.");

        if (vigenteHasta.HasValue &&
            vigenteHasta.Value < vigenteDesde)
        {
            throw new ArgumentException(
                "VigenteHasta no puede ser anterior a VigenteDesde.");
        }

        if (horaFin <= horaInicio)
            throw new ArgumentException(
                "HoraFin debe ser posterior a HoraInicio.");
    }

    private static HorarioResponse ToResponse(
        HorarioClase horario)
    {
        return new HorarioResponse
        {
            Id = horario.Id,
            AsignaturaId = horario.AsignaturaId,
            GrupoId = horario.GrupoId,
            DocenteId = horario.DocenteId,
            AulaId = horario.AulaId,
            DiaSemana = horario.DiaSemana,
            VigenteDesde = horario.VigenteDesde,
            VigenteHasta = horario.VigenteHasta,
            HoraInicio = horario.HoraInicio,
            HoraFin = horario.HoraFin,
            Anulado = horario.Anulado
        };
    }
}