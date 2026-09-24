using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminClassroomService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    public AdminClassroomService(
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<ClassroomResponse> CreateAsync(
        CreateClassroomRequest request,
        ClaimsPrincipal user)
    {
        ValidateData(request.Codigo, request.Nombre);

        var codigo = request.Codigo.Trim();

        var exists = await _db.Aulas
            .AnyAsync(a => a.Codigo == codigo);

        if (exists)
            throw new InvalidOperationException(
                "El código del aula ya existe.");

        var classroom = new Aula
        {
            Codigo = codigo,
            Nombre = request.Nombre.Trim(),
            Habilitada = true
        };

        _db.Aulas.Add(classroom);

        _auditService.Registrar(
            user,
            "Aula",
            "NUEVA",
            "CREAR",
            "Creación de aula.",
            antes: null,
            despues: new
            {
                classroom.Codigo,
                classroom.Nombre,
                classroom.Habilitada
            });

        await _db.SaveChangesAsync();

        return BuildResponse(classroom);
    }

    public async Task<List<ClassroomResponse>> GetAllAsync()
    {
        return await _db.Aulas
            .AsNoTracking()
            .OrderBy(a => a.Codigo)
            .Select(a => new ClassroomResponse
            {
                Id = a.Id,
                Codigo = a.Codigo,
                Nombre = a.Nombre,
                Habilitada = a.Habilitada
            })
            .ToListAsync();
    }

    public async Task<ClassroomResponse> GetByIdAsync(
        long id)
    {
        var classroom = await _db.Aulas
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (classroom is null)
            throw new KeyNotFoundException(
                "El aula no existe.");

        return BuildResponse(classroom);
    }

    public async Task<ClassroomResponse> UpdateAsync(
        long id,
        UpdateClassroomRequest request,
        ClaimsPrincipal user)
    {
        ValidateData(request.Codigo, request.Nombre);

        var classroom = await _db.Aulas
            .FirstOrDefaultAsync(a => a.Id == id);

        if (classroom is null)
            throw new KeyNotFoundException(
                "El aula no existe.");

        var codigo = request.Codigo.Trim();

        var duplicate = await _db.Aulas
            .AnyAsync(a =>
                a.Id != id &&
                a.Codigo == codigo);

        if (duplicate)
            throw new InvalidOperationException(
                "El código del aula ya existe.");

        var antes = new
        {
            classroom.Id,
            classroom.Codigo,
            classroom.Nombre,
            classroom.Habilitada
        };

        classroom.Codigo = codigo;
        classroom.Nombre = request.Nombre.Trim();

        _auditService.Registrar(
            user,
            "Aula",
            classroom.Id.ToString(),
            "ACTUALIZAR",
            "Actualización de aula.",
            antes: antes,
            despues: new
            {
                classroom.Id,
                classroom.Codigo,
                classroom.Nombre,
                classroom.Habilitada
            });

        await _db.SaveChangesAsync();

        return BuildResponse(classroom);
    }

    public async Task SetStatusAsync(
        long id,
        bool habilitada,
        ClaimsPrincipal user)
    {
        var classroom = await _db.Aulas
            .FirstOrDefaultAsync(a => a.Id == id);

        if (classroom is null)
            throw new KeyNotFoundException(
                "El aula no existe.");

        if (classroom.Habilitada == habilitada)
            throw new InvalidOperationException(
                habilitada
                    ? "El aula ya está habilitada."
                    : "El aula ya está deshabilitada.");

        var estadoAnterior = classroom.Habilitada;

        classroom.Habilitada = habilitada;

        _auditService.Registrar(
            user,
            "Aula",
            classroom.Id.ToString(),
            habilitada ? "HABILITAR" : "DESHABILITAR",
            habilitada
                ? "Habilitación de aula."
                : "Deshabilitación de aula.",
            antes: new
            {
                classroom.Id,
                Habilitada = estadoAnterior
            },
            despues: new
            {
                classroom.Id,
                classroom.Habilitada
            });

        await _db.SaveChangesAsync();
    }

    private static void ValidateData(
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

    private static ClassroomResponse BuildResponse(
        Aula classroom)
    {
        return new ClassroomResponse
        {
            Id = classroom.Id,
            Codigo = classroom.Codigo,
            Nombre = classroom.Nombre,
            Habilitada = classroom.Habilitada
        };
    }
}