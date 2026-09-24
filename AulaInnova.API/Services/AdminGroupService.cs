using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminGroupService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    public AdminGroupService(
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<GroupResponse> CreateAsync(
        CreateGroupRequest request,
        ClaimsPrincipal user)
    {
        Validate(request);

        var duplicate = await _db.Grupos.AnyAsync(g =>
            g.Ciclo == request.Ciclo &&
            g.Grado == request.Grado.Trim() &&
            g.Seccion == request.Seccion.Trim() &&
            g.Desde == request.Desde);

        if (duplicate)
            throw new InvalidOperationException(
                "El grupo ya existe.");

        var group = new Grupo
        {
            Ciclo = request.Ciclo,
            Grado = request.Grado.Trim(),
            Seccion = request.Seccion.Trim(),
            Desde = request.Desde,
            Hasta = request.Hasta,
            Habilitado = true
        };

        _db.Grupos.Add(group);

        _auditService.Registrar(
            user,
            "Grupo",
            "NUEVO",
            "CREAR",
            "Creación de grupo.",
            antes: null,
            despues: new
            {
                group.Ciclo,
                group.Grado,
                group.Seccion,
                group.Desde,
                group.Hasta,
                group.Habilitado
            });

        await _db.SaveChangesAsync();

        return BuildResponse(group);
    }

    public async Task<List<GroupResponse>> GetAllAsync()
    {
        return await _db.Grupos
            .AsNoTracking()
            .OrderBy(g => g.Ciclo)
            .ThenBy(g => g.Grado)
            .ThenBy(g => g.Seccion)
            .Select(g => new GroupResponse
            {
                Id = g.Id,
                Ciclo = g.Ciclo,
                Grado = g.Grado,
                Seccion = g.Seccion,
                Desde = g.Desde,
                Hasta = g.Hasta,
                Habilitado = g.Habilitado
            })
            .ToListAsync();
    }

    public async Task<GroupResponse> GetByIdAsync(long id)
    {
        var group = await _db.Grupos
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null)
            throw new KeyNotFoundException(
                "El grupo no existe.");

        return BuildResponse(group);
    }

    public async Task<GroupResponse> UpdateAsync(
        long id,
        UpdateGroupRequest request,
        ClaimsPrincipal user)
    {
        Validate(request);

        var group = await _db.Grupos
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null)
            throw new KeyNotFoundException(
                "El grupo no existe.");

        var duplicate = await _db.Grupos.AnyAsync(g =>
            g.Id != id &&
            g.Ciclo == request.Ciclo &&
            g.Grado == request.Grado.Trim() &&
            g.Seccion == request.Seccion.Trim() &&
            g.Desde == request.Desde);

        if (duplicate)
            throw new InvalidOperationException(
                "El grupo ya existe.");

        var antes = new
        {
            group.Id,
            group.Ciclo,
            group.Grado,
            group.Seccion,
            group.Desde,
            group.Hasta,
            group.Habilitado
        };

        group.Ciclo = request.Ciclo;
        group.Grado = request.Grado.Trim();
        group.Seccion = request.Seccion.Trim();
        group.Desde = request.Desde;
        group.Hasta = request.Hasta;

        _auditService.Registrar(
            user,
            "Grupo",
            group.Id.ToString(),
            "ACTUALIZAR",
            "Actualización de grupo.",
            antes: antes,
            despues: new
            {
                group.Id,
                group.Ciclo,
                group.Grado,
                group.Seccion,
                group.Desde,
                group.Hasta,
                group.Habilitado
            });

        await _db.SaveChangesAsync();

        return BuildResponse(group);
    }

    public async Task SetStatusAsync(
        long id,
        bool habilitado,
        ClaimsPrincipal user)
    {
        var group = await _db.Grupos
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null)
            throw new KeyNotFoundException(
                "El grupo no existe.");

        if (group.Habilitado == habilitado)
            throw new InvalidOperationException(
                habilitado
                    ? "El grupo ya está habilitado."
                    : "El grupo ya está deshabilitado.");

        var estadoAnterior = group.Habilitado;

        group.Habilitado = habilitado;

        _auditService.Registrar(
            user,
            "Grupo",
            group.Id.ToString(),
            habilitado ? "HABILITAR" : "DESHABILITAR",
            habilitado
                ? "Habilitación de grupo."
                : "Deshabilitación de grupo.",
            antes: new
            {
                group.Id,
                Habilitado = estadoAnterior
            },
            despues: new
            {
                group.Id,
                group.Habilitado
            });

        await _db.SaveChangesAsync();
    }

    private static void Validate(CreateGroupRequest request)
    {
        ValidateData(
            request.Ciclo,
            request.Grado,
            request.Seccion,
            request.Desde,
            request.Hasta);
    }

    private static void Validate(UpdateGroupRequest request)
    {
        ValidateData(
            request.Ciclo,
            request.Grado,
            request.Seccion,
            request.Desde,
            request.Hasta);
    }

    private static void ValidateData(
        short ciclo,
        string grado,
        string seccion,
        DateOnly desde,
        DateOnly hasta)
    {
        if (ciclo <= 0 ||
            string.IsNullOrWhiteSpace(grado) ||
            string.IsNullOrWhiteSpace(seccion))
        {
            throw new InvalidOperationException(
                "Ciclo, grado y sección son obligatorios.");
        }

        if (hasta < desde)
        {
            throw new InvalidOperationException(
                "La fecha final no puede ser anterior a la inicial.");
        }
    }

    private static GroupResponse BuildResponse(Grupo group)
    {
        return new GroupResponse
        {
            Id = group.Id,
            Ciclo = group.Ciclo,
            Grado = group.Grado,
            Seccion = group.Seccion,
            Desde = group.Desde,
            Hasta = group.Hasta,
            Habilitado = group.Habilitado
        };
    }
}