using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin.Matricula;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminMatriculaService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    public AdminMatriculaService(
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<MatriculaResponse> CreateAsync(
        CreateMatriculaRequest request,
        ClaimsPrincipal user)
    {
        ValidateDates(request.Desde, request.Hasta);

        var estudiante = await _db.Estudiantes
            .Include(e => e.Persona)
            .FirstOrDefaultAsync(e => e.PersonaId == request.EstudianteId);

        if (estudiante is null || !estudiante.Persona.Habilitada)
            throw new ArgumentException(
                "El estudiante no existe o está deshabilitado.");

        var grupo = await _db.Grupos
            .FirstOrDefaultAsync(g => g.Id == request.GrupoId);

        if (grupo is null || !grupo.Habilitado)
            throw new ArgumentException(
                "El grupo no existe o está deshabilitado.");

        var conflict = await _db.Matriculas.AnyAsync(m =>
            m.EstudianteId == request.EstudianteId &&
            !m.Anulada &&
            m.Desde <= (request.Hasta ?? DateOnly.MaxValue) &&
            (m.Hasta == null || m.Hasta >= request.Desde));

        if (conflict)
            throw new InvalidOperationException(
                "El estudiante ya tiene una matrícula vigente o superpuesta.");

        var matricula = new Matricula
        {
            EstudianteId = request.EstudianteId,
            GrupoId = request.GrupoId,
            Desde = request.Desde,
            Hasta = request.Hasta,
            Anulada = false,
            CreadoUtc = DateTime.UtcNow
        };

        _db.Matriculas.Add(matricula);

        _auditService.Registrar(
            user,
            "Matricula",
            "NUEVA",
            "CREAR",
            "Alta de matrícula.",
            antes: null,
            despues: new
            {
                matricula.EstudianteId,
                matricula.GrupoId,
                matricula.Desde,
                matricula.Hasta,
                matricula.Anulada
            });

        await _db.SaveChangesAsync();

        return ToResponse(matricula);
    }

    public async Task<List<MatriculaResponse>> GetAllAsync()
    {
        return await _db.Matriculas
            .AsNoTracking()
            .OrderByDescending(m => m.Desde)
            .Select(m => new MatriculaResponse
            {
                Id = m.Id,
                EstudianteId = m.EstudianteId,
                GrupoId = m.GrupoId,
                Desde = m.Desde,
                Hasta = m.Hasta,
                Anulada = m.Anulada
            })
            .ToListAsync();
    }

    public async Task<MatriculaResponse?> GetByIdAsync(long id)
    {
        var matricula = await _db.Matriculas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        return matricula is null
            ? null
            : ToResponse(matricula);
    }

    public async Task<MatriculaResponse> UpdateAsync(
        long id,
        UpdateMatriculaRequest request,
        ClaimsPrincipal user)
    {
        ValidateDates(request.Desde, request.Hasta);

        var matricula = await _db.Matriculas
            .FirstOrDefaultAsync(m => m.Id == id);

        if (matricula is null)
            throw new KeyNotFoundException(
                "Matrícula no encontrada.");

        var antes = new
        {
            matricula.Id,
            matricula.EstudianteId,
            matricula.GrupoId,
            matricula.Desde,
            matricula.Hasta,
            matricula.Anulada
        };

        var grupo = await _db.Grupos
            .FirstOrDefaultAsync(g => g.Id == request.GrupoId);

        if (grupo is null || !grupo.Habilitado)
            throw new ArgumentException(
                "El grupo no existe o está deshabilitado.");

        var conflict = await _db.Matriculas.AnyAsync(m =>
            m.Id != id &&
            m.EstudianteId == matricula.EstudianteId &&
            !m.Anulada &&
            m.Desde <= (request.Hasta ?? DateOnly.MaxValue) &&
            (m.Hasta == null || m.Hasta >= request.Desde));

        if (conflict)
            throw new InvalidOperationException(
                "El estudiante ya tiene otra matrícula vigente o superpuesta.");

        matricula.GrupoId = request.GrupoId;
        matricula.Desde = request.Desde;
        matricula.Hasta = request.Hasta;

        _auditService.Registrar(
            user,
            "Matricula",
            matricula.Id.ToString(),
            "ACTUALIZAR",
            "Actualización de matrícula.",
            antes: antes,
            despues: new
            {
                matricula.Id,
                matricula.EstudianteId,
                matricula.GrupoId,
                matricula.Desde,
                matricula.Hasta,
                matricula.Anulada
            });

        await _db.SaveChangesAsync();

        return ToResponse(matricula);
    }

    public async Task SetStatusAsync(
        long id,
        bool anulada,
        ClaimsPrincipal user)
    {
        var matricula = await _db.Matriculas
            .FirstOrDefaultAsync(m => m.Id == id);

        if (matricula is null)
            throw new KeyNotFoundException(
                "Matrícula no encontrada.");

        var antes = new
        {
            matricula.Id,
            matricula.EstudianteId,
            matricula.GrupoId,
            matricula.Desde,
            matricula.Hasta,
            matricula.Anulada
        };

        matricula.Anulada = anulada;

        _auditService.Registrar(
            user,
            "Matricula",
            matricula.Id.ToString(),
            anulada ? "ANULAR" : "REACTIVAR",
            anulada
                ? "Anulación de matrícula."
                : "Reactivación de matrícula.",
            antes: antes,
            despues: new
            {
                matricula.Id,
                matricula.EstudianteId,
                matricula.GrupoId,
                matricula.Desde,
                matricula.Hasta,
                matricula.Anulada
            });

        await _db.SaveChangesAsync();
    }

    private static void ValidateDates(
        DateOnly desde,
        DateOnly? hasta)
    {
        if (hasta.HasValue && hasta.Value < desde)
            throw new ArgumentException(
                "Hasta no puede ser anterior a Desde.");
    }

    private static MatriculaResponse ToResponse(
        Matricula matricula)
    {
        return new MatriculaResponse
        {
            Id = matricula.Id,
            EstudianteId = matricula.EstudianteId,
            GrupoId = matricula.GrupoId,
            Desde = matricula.Desde,
            Hasta = matricula.Hasta,
            Anulada = matricula.Anulada
        };
    }
}