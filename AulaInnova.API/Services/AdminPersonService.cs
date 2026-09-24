using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminPersonService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    public AdminPersonService(
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<PersonResponse> CreateStudentAsync(
        CreateStudentRequest request,
        ClaimsPrincipal user)
    {
        ValidateBasicData(
            request.Nombres,
            request.Apellidos,
            request.Codigo);

        var codeExists = await _db.Estudiantes
            .AnyAsync(e => e.Codigo == request.Codigo.Trim());

        if (codeExists)
            throw new InvalidOperationException(
                "El código del estudiante ya existe.");

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var person = new Persona
        {
            TipoPersona = "ESTUDIANTE",
            Nombres = request.Nombres.Trim(),
            Apellidos = request.Apellidos.Trim(),
            Habilitada = true,
            CreadoUtc = DateTime.UtcNow
        };

        _db.Personas.Add(person);

        await _db.SaveChangesAsync();

        var student = new Estudiante
        {
            PersonaId = person.Id,
            TipoPersona = "ESTUDIANTE",
            Codigo = request.Codigo.Trim()
        };

        _db.Estudiantes.Add(student);

        _auditService.Registrar(
            user,
            "Persona",
            person.Id.ToString(),
            "CREAR",
            "Creación de persona estudiante.",
            antes: null,
            despues: new
            {
                PersonaId = person.Id,
                person.TipoPersona,
                person.Nombres,
                person.Apellidos,
                person.Habilitada,
                student.Codigo
            });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return await BuildResponseAsync(person);
    }

    public async Task<PersonResponse> CreateStaffAsync(
        CreateStaffRequest request,
        ClaimsPrincipal user)
    {
        ValidateBasicData(
            request.Nombres,
            request.Apellidos,
            request.Codigo);

        var codeExists = await _db.Personal
            .AnyAsync(p => p.Codigo == request.Codigo.Trim());

        if (codeExists)
            throw new InvalidOperationException(
                "El código del personal ya existe.");

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var person = new Persona
        {
            TipoPersona = "PERSONAL",
            Nombres = request.Nombres.Trim(),
            Apellidos = request.Apellidos.Trim(),
            Habilitada = true,
            CreadoUtc = DateTime.UtcNow
        };

        _db.Personas.Add(person);

        await _db.SaveChangesAsync();

        var staff = new Personal
        {
            PersonaId = person.Id,
            TipoPersona = "PERSONAL",
            Codigo = request.Codigo.Trim()
        };

        _db.Personal.Add(staff);

        _auditService.Registrar(
            user,
            "Persona",
            person.Id.ToString(),
            "CREAR",
            "Creación de persona de personal.",
            antes: null,
            despues: new
            {
                PersonaId = person.Id,
                person.TipoPersona,
                person.Nombres,
                person.Apellidos,
                person.Habilitada,
                staff.Codigo
            });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return await BuildResponseAsync(person);
    }

    public async Task<List<PersonResponse>> GetAllAsync()
    {
        var people = await _db.Personas
            .AsNoTracking()
            .Include(p => p.Estudiante)
            .Include(p => p.Personal)
            .Include(p => p.Usuario)
            .OrderBy(p => p.Apellidos)
            .ThenBy(p => p.Nombres)
            .ToListAsync();

        return people
            .Select(BuildResponse)
            .ToList();
    }

    public async Task<PersonResponse> GetByIdAsync(long id)
    {
        var person = await _db.Personas
            .AsNoTracking()
            .Include(p => p.Estudiante)
            .Include(p => p.Personal)
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null)
            throw new KeyNotFoundException(
                "La persona no existe.");

        return BuildResponse(person);
    }

    public async Task<PersonResponse> UpdateAsync(
        long id,
        UpdatePersonRequest request,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Nombres) ||
            string.IsNullOrWhiteSpace(request.Apellidos))
        {
            throw new InvalidOperationException(
                "Nombres y apellidos son obligatorios.");
        }

        var person = await _db.Personas
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null)
            throw new KeyNotFoundException(
                "La persona no existe.");

        var antes = new
        {
            person.Id,
            person.TipoPersona,
            person.Nombres,
            person.Apellidos,
            person.Habilitada
        };

        person.Nombres = request.Nombres.Trim();
        person.Apellidos = request.Apellidos.Trim();

        _auditService.Registrar(
            user,
            "Persona",
            person.Id.ToString(),
            "ACTUALIZAR",
            "Actualización de datos de persona.",
            antes: antes,
            despues: new
            {
                person.Id,
                person.TipoPersona,
                person.Nombres,
                person.Apellidos,
                person.Habilitada
            });

        await _db.SaveChangesAsync();

        return await BuildResponseAsync(person);
    }

    public async Task SetStatusAsync(
        long id,
        bool habilitada,
        ClaimsPrincipal user)
    {
        var person = await _db.Personas
            .FirstOrDefaultAsync(p => p.Id == id);

        if (person is null)
            throw new KeyNotFoundException(
                "La persona no existe.");

        if (person.Habilitada == habilitada)
            throw new InvalidOperationException(
                habilitada
                    ? "La persona ya está habilitada."
                    : "La persona ya está deshabilitada.");

        var estadoAnterior = person.Habilitada;

        person.Habilitada = habilitada;

        _auditService.Registrar(
            user,
            "Persona",
            person.Id.ToString(),
            habilitada ? "HABILITAR" : "DESHABILITAR",
            habilitada
                ? "Habilitación de persona."
                : "Deshabilitación de persona.",
            antes: new
            {
                person.Id,
                Habilitada = estadoAnterior
            },
            despues: new
            {
                person.Id,
                person.Habilitada
            });

        await _db.SaveChangesAsync();
    }

    private async Task<PersonResponse> BuildResponseAsync(
        Persona person)
    {
        var result = await _db.Personas
            .AsNoTracking()
            .Include(p => p.Estudiante)
            .Include(p => p.Personal)
            .Include(p => p.Usuario)
            .FirstAsync(p => p.Id == person.Id);

        return BuildResponse(result);
    }

    private static PersonResponse BuildResponse(
        Persona person)
    {
        var code = person.Estudiante?.Codigo
                   ?? person.Personal?.Codigo
                   ?? string.Empty;

        return new PersonResponse
        {
            Id = person.Id,
            TipoPersona = person.TipoPersona,
            Nombres = person.Nombres,
            Apellidos = person.Apellidos,
            Codigo = code,
            Habilitada = person.Habilitada,
            UsuarioId = person.Usuario?.Id
        };
    }

    private static void ValidateBasicData(
        string nombres,
        string apellidos,
        string codigo)
    {
        if (string.IsNullOrWhiteSpace(nombres) ||
            string.IsNullOrWhiteSpace(apellidos) ||
            string.IsNullOrWhiteSpace(codigo))
        {
            throw new InvalidOperationException(
                "Nombres, apellidos y código son obligatorios.");
        }
    }
}