using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin.Estacion;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminFallaEstacionService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    public AdminFallaEstacionService(
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<FallaEstacionResponse> CreateAsync(
        CreateFallaEstacionRequest request,
        Guid usuarioId,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Descripcion))
            throw new ArgumentException(
                "La descripción de la falla es obligatoria.");

        var estacion = await _db.Estaciones
            .FirstOrDefaultAsync(e => e.Id == request.EstacionId);

        if (estacion is null)
            throw new KeyNotFoundException(
                "Estación no encontrada.");

        var fallaAbierta = await _db.FallasEstacion
            .AnyAsync(f =>
                f.EstacionId == request.EstacionId &&
                f.Estado == "ABIERTA");

        if (fallaAbierta)
            throw new InvalidOperationException(
                "La estación ya tiene una falla abierta.");

        var falla = new FallaEstacion
        {
            EstacionId = request.EstacionId,
            ReportadoPor = usuarioId,
            Descripcion = request.Descripcion.Trim(),
            Estado = "ABIERTA",
            ResueltaUtc = null,
            CreadoUtc = DateTime.UtcNow
        };

        _db.FallasEstacion.Add(falla);

        _auditService.Registrar(
            user,
            "FallaEstacion",
            "NUEVA",
            "CREAR",
            "Reporte de falla de estación.",
            antes: null,
            despues: new
            {
                falla.EstacionId,
                falla.ReportadoPor,
                falla.Descripcion,
                falla.Estado,
                falla.ResueltaUtc
            });

        await _db.SaveChangesAsync();

        return ToResponse(falla);
    }

    public async Task<List<FallaEstacionResponse>> GetAllAsync()
    {
        return await _db.FallasEstacion
            .AsNoTracking()
            .OrderByDescending(f => f.CreadoUtc)
            .Select(f => new FallaEstacionResponse
            {
                Id = f.Id,
                EstacionId = f.EstacionId,
                ReportadoPor = f.ReportadoPor,
                Descripcion = f.Descripcion,
                Estado = f.Estado,
                ResueltaUtc = f.ResueltaUtc,
                CreadoUtc = f.CreadoUtc
            })
            .ToListAsync();
    }

    public async Task<FallaEstacionResponse?> GetByIdAsync(long id)
    {
        var falla = await _db.FallasEstacion
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        return falla is null
            ? null
            : ToResponse(falla);
    }

    public async Task CloseAsync(
        long id,
        ClaimsPrincipal user)
    {
        var falla = await _db.FallasEstacion
            .FirstOrDefaultAsync(f => f.Id == id);

        if (falla is null)
            throw new KeyNotFoundException(
                "Falla de estación no encontrada.");

        if (falla.Estado != "ABIERTA")
            throw new InvalidOperationException(
                "La falla ya está cerrada.");

        var antes = new
        {
            falla.Id,
            falla.EstacionId,
            falla.ReportadoPor,
            falla.Descripcion,
            falla.Estado,
            falla.ResueltaUtc
        };

        falla.Estado = "CERRADA";
        falla.ResueltaUtc = DateTime.UtcNow;

        _auditService.Registrar(
            user,
            "FallaEstacion",
            falla.Id.ToString(),
            "CERRAR",
            "Cierre de falla de estación.",
            antes: antes,
            despues: new
            {
                falla.Id,
                falla.EstacionId,
                falla.ReportadoPor,
                falla.Descripcion,
                falla.Estado,
                falla.ResueltaUtc
            });

        await _db.SaveChangesAsync();
    }

    private static FallaEstacionResponse ToResponse(
        FallaEstacion falla)
    {
        return new FallaEstacionResponse
        {
            Id = falla.Id,
            EstacionId = falla.EstacionId,
            ReportadoPor = falla.ReportadoPor,
            Descripcion = falla.Descripcion,
            Estado = falla.Estado,
            ResueltaUtc = falla.ResueltaUtc,
            CreadoUtc = falla.CreadoUtc
        };
    }
}