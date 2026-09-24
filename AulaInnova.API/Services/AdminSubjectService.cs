using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminSubjectService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    public AdminSubjectService(
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<SubjectResponse> CreateAsync(
        CreateSubjectRequest request,
        ClaimsPrincipal user)
    {
        Validate(request.Codigo, request.Nombre);

        var codigo = request.Codigo.Trim();

        if (await _db.Asignaturas.AnyAsync(a => a.Codigo == codigo))
            throw new InvalidOperationException(
                "El código de la asignatura ya existe.");

        var subject = new Asignatura
        {
            Codigo = codigo,
            Nombre = request.Nombre.Trim(),
            Habilitada = true
        };

        _db.Asignaturas.Add(subject);

        _auditService.Registrar(
            user,
            "Asignatura",
            "NUEVA",
            "CREAR",
            "Creación de asignatura.",
            antes: null,
            despues: new
            {
                subject.Codigo,
                subject.Nombre,
                subject.Habilitada
            });

        await _db.SaveChangesAsync();

        return BuildResponse(subject);
    }

    public async Task<List<SubjectResponse>> GetAllAsync()
    {
        return await _db.Asignaturas
            .AsNoTracking()
            .OrderBy(a => a.Codigo)
            .Select(a => new SubjectResponse
            {
                Id = a.Id,
                Codigo = a.Codigo,
                Nombre = a.Nombre,
                Habilitada = a.Habilitada
            })
            .ToListAsync();
    }

    public async Task<SubjectResponse> GetByIdAsync(long id)
    {
        var subject = await _db.Asignaturas
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (subject is null)
            throw new KeyNotFoundException(
                "La asignatura no existe.");

        return BuildResponse(subject);
    }

    public async Task<SubjectResponse> UpdateAsync(
        long id,
        UpdateSubjectRequest request,
        ClaimsPrincipal user)
    {
        Validate(request.Codigo, request.Nombre);

        var subject = await _db.Asignaturas
            .FirstOrDefaultAsync(a => a.Id == id);

        if (subject is null)
            throw new KeyNotFoundException(
                "La asignatura no existe.");

        var codigo = request.Codigo.Trim();

        if (await _db.Asignaturas.AnyAsync(
                a => a.Id != id && a.Codigo == codigo))
        {
            throw new InvalidOperationException(
                "El código de la asignatura ya existe.");
        }

        var antes = new
        {
            subject.Id,
            subject.Codigo,
            subject.Nombre,
            subject.Habilitada
        };

        subject.Codigo = codigo;
        subject.Nombre = request.Nombre.Trim();

        _auditService.Registrar(
            user,
            "Asignatura",
            subject.Id.ToString(),
            "ACTUALIZAR",
            "Actualización de asignatura.",
            antes: antes,
            despues: new
            {
                subject.Id,
                subject.Codigo,
                subject.Nombre,
                subject.Habilitada
            });

        await _db.SaveChangesAsync();

        return BuildResponse(subject);
    }

    public async Task SetStatusAsync(
        long id,
        bool habilitada,
        ClaimsPrincipal user)
    {
        var subject = await _db.Asignaturas
            .FirstOrDefaultAsync(a => a.Id == id);

        if (subject is null)
            throw new KeyNotFoundException(
                "La asignatura no existe.");

        if (subject.Habilitada == habilitada)
            throw new InvalidOperationException(
                habilitada
                    ? "La asignatura ya está habilitada."
                    : "La asignatura ya está deshabilitada.");

        var estadoAnterior = subject.Habilitada;

        subject.Habilitada = habilitada;

        _auditService.Registrar(
            user,
            "Asignatura",
            subject.Id.ToString(),
            habilitada ? "HABILITAR" : "DESHABILITAR",
            habilitada
                ? "Habilitación de asignatura."
                : "Deshabilitación de asignatura.",
            antes: new
            {
                subject.Id,
                Habilitada = estadoAnterior
            },
            despues: new
            {
                subject.Id,
                subject.Habilitada
            });

        await _db.SaveChangesAsync();
    }

    private static void Validate(
        string codigo,
        string nombre)
    {
        if (string.IsNullOrWhiteSpace(codigo) ||
            string.IsNullOrWhiteSpace(nombre))
        {
            throw new InvalidOperationException(
                "Código y nombre son obligatorios.");
        }
    }

    private static SubjectResponse BuildResponse(
        Asignatura subject)
    {
        return new SubjectResponse
        {
            Id = subject.Id,
            Codigo = subject.Codigo,
            Nombre = subject.Nombre,
            Habilitada = subject.Habilitada
        };
    }
}