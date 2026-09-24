using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Coverage;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class CoverageService
{
    private readonly AulaInnovaDbContext _context;
    private readonly AuthorizationService _authorizationService;
    private readonly AuditService _auditService;

    public CoverageService(
        AulaInnovaDbContext context,
        AuthorizationService authorizationService,
        AuditService auditService)
    {
        _context = context;
        _authorizationService = authorizationService;
        _auditService = auditService;
    }

    public async Task<CoverageResponse?> CreateNeedAsync(
        CreateCoverageNeedRequest request,
        ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return null;

        var asignacionOrigen = await _context.Asignaciones
            .FirstOrDefaultAsync(a =>
                a.Id == request.AsignacionOrigenId &&
                a.Estado == "ACTIVA");

        if (asignacionOrigen is null)
            return null;

        var autorizado =
            await _authorizationService.CanAccessAssignmentAsync(
                user,
                "COBERTURA_LEER",
                asignacionOrigen.Id);

        if (!autorizado)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para gestionar esta necesidad de cobertura.");
        }

        if (string.IsNullOrWhiteSpace(request.Motivo))
            return null;

        var existente = await _context.NecesidadesCobertura
            .FirstOrDefaultAsync(n =>
                n.AsignacionOrigenId ==
                asignacionOrigen.Id);

        if (existente is not null)
        {
            return new CoverageResponse
            {
                NecesidadCoberturaId = existente.Id,
                AsignacionOrigenId = existente.AsignacionOrigenId,
                AsignacionCoberturaId =
                    existente.AsignacionCoberturaId,
                Estado = existente.Estado,
                CreadoUtc = existente.CreadoUtc
            };
        }

        var necesidad = new NecesidadCobertura
        {
            AsignacionOrigenId = asignacionOrigen.Id,
            AsignacionCoberturaId = null,
            Estado = "PENDIENTE",
            Motivo = request.Motivo.Trim(),
            CreadaPor = userId,
            CreadoUtc = DateTime.UtcNow
        };

        _context.NecesidadesCobertura.Add(necesidad);

        await _context.SaveChangesAsync();

        _auditService.Registrar(
            user,
            "NecesidadCobertura",
            necesidad.Id.ToString(),
            "CREAR",
            "Creación de necesidad de cobertura.",
            antes: null,
            despues: new
            {
                necesidad.Id,
                necesidad.AsignacionOrigenId,
                necesidad.AsignacionCoberturaId,
                necesidad.Estado,
                necesidad.Motivo,
                necesidad.CreadaPor,
                necesidad.CreadoUtc
            });

        await _context.SaveChangesAsync();

        return new CoverageResponse
        {
            NecesidadCoberturaId = necesidad.Id,
            AsignacionOrigenId = necesidad.AsignacionOrigenId,
            AsignacionCoberturaId =
                necesidad.AsignacionCoberturaId,
            Estado = necesidad.Estado,
            CreadoUtc = necesidad.CreadoUtc
        };
    }

    public async Task<CoverageResponse?> AssignAsync(
        AssignCoverageRequest request,
        ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return null;

        var necesidad = await _context.NecesidadesCobertura
            .FirstOrDefaultAsync(n =>
                n.Id == request.NecesidadCoberturaId);

        if (necesidad is null ||
            necesidad.Estado != "PENDIENTE")
        {
            return null;
        }

        var asignacionCobertura = await _context.Asignaciones
            .FirstOrDefaultAsync(a =>
                a.Id == request.AsignacionCoberturaId &&
                a.Estado == "ACTIVA");

        if (asignacionCobertura is null)
            return null;

        if (asignacionCobertura.Id ==
            necesidad.AsignacionOrigenId)
        {
            return null;
        }

        var asignacionOrigen = await _context.Asignaciones
            .FirstOrDefaultAsync(a =>
                a.Id == necesidad.AsignacionOrigenId);

        if (asignacionOrigen is null)
            return null;

        var autorizadoOrigen =
            await _authorizationService.CanAccessAssignmentAsync(
                user,
                "COBERTURA_LEER",
                asignacionOrigen.Id);

        if (!autorizadoOrigen)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para gestionar esta necesidad de cobertura.");
        }

        var autorizadoCobertura =
            await _authorizationService.CanAccessAssignmentAsync(
                user,
                "COBERTURA_LEER",
                asignacionCobertura.Id);

        if (!autorizadoCobertura)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para utilizar esta asignación como cobertura.");
        }

        if (asignacionCobertura.JornadaId !=
            asignacionOrigen.JornadaId)
        {
            return null;
        }

        var antes = new
        {
            necesidad.Id,
            necesidad.AsignacionOrigenId,
            necesidad.AsignacionCoberturaId,
            necesidad.Estado
        };

        necesidad.AsignacionCoberturaId =
            asignacionCobertura.Id;

        necesidad.Estado = "ASIGNADA";

        await _context.SaveChangesAsync();

        _auditService.Registrar(
            user,
            "NecesidadCobertura",
            necesidad.Id.ToString(),
            "ASIGNAR",
            "Asignación de cobertura.",
            antes: antes,
            despues: new
            {
                necesidad.Id,
                necesidad.AsignacionOrigenId,
                necesidad.AsignacionCoberturaId,
                necesidad.Estado
            });

        await _context.SaveChangesAsync();

        return new CoverageResponse
        {
            NecesidadCoberturaId = necesidad.Id,
            AsignacionOrigenId =
                necesidad.AsignacionOrigenId,
            AsignacionCoberturaId =
                necesidad.AsignacionCoberturaId,
            Estado = necesidad.Estado,
            CreadoUtc = necesidad.CreadoUtc
        };
    }

    public async Task<CoverageResponse?> VerifyAsync(
        VerifyCoverageRequest request,
        ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return null;

        if (request.Estado != "PRESENCIA_VERIFICADA" &&
            request.Estado != "AUSENCIA_VERIFICADA")
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.Observacion))
            return null;

        if (request.OcurridoUtc.HasValue &&
            request.OcurridoUtc.Value.Kind != DateTimeKind.Utc)
        {
            return null;
        }

        var asignacion = await _context.Asignaciones
            .FirstOrDefaultAsync(a =>
                a.Id == request.AsignacionId);

        if (asignacion is null)
            return null;

        var autorizado =
            await _authorizationService.CanAccessAssignmentAsync(
                user,
                "COBERTURA_VERIFICAR",
                asignacion.Id);

        if (!autorizado)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para verificar esta cobertura.");
        }

        var necesidad = await _context.NecesidadesCobertura
            .FirstOrDefaultAsync(n =>
                n.AsignacionOrigenId == asignacion.Id &&
                n.Estado == "ASIGNADA");

        if (necesidad is null)
            return null;

        var ocurridoUtc =
            request.OcurridoUtc ?? DateTime.UtcNow;

        var antes = new
        {
            necesidad.Id,
            necesidad.AsignacionOrigenId,
            necesidad.AsignacionCoberturaId,
            necesidad.Estado,
            necesidad.CerradoPor,
            necesidad.CerradoUtc
        };

        var verificacion = new VerificacionCobertura
        {
            AsignacionId = asignacion.Id,
            VerificadoPor = userId,
            Estado = request.Estado,
            Observacion = request.Observacion.Trim(),
            OcurridoUtc = ocurridoUtc,
            CreadoUtc = DateTime.UtcNow
        };

        _context.VerificacionesCobertura.Add(verificacion);

        if (request.Estado == "PRESENCIA_VERIFICADA")
        {
            necesidad.Estado = "CERRADA";
            necesidad.CerradoPor = userId;
            necesidad.CerradoUtc = DateTime.UtcNow;
        }
        else
        {
            necesidad.Estado = "PENDIENTE";
            necesidad.AsignacionCoberturaId = null;
            necesidad.CerradoPor = null;
            necesidad.CerradoUtc = null;
        }

        await _context.SaveChangesAsync();

        _auditService.Registrar(
             user,
             "NecesidadCobertura",
             necesidad.Id.ToString(),
             request.Estado == "PRESENCIA_VERIFICADA"
                 ? "VERIFICAR"
                 : "REABRIR",
             request.Estado == "PRESENCIA_VERIFICADA"
                 ? "Verificación de presencia y cierre de cobertura."
                 : "Verificación de ausencia y reapertura de necesidad de cobertura.",
             antes: antes,
             despues: new
             {
                 necesidad.Id,
                 necesidad.AsignacionOrigenId,
                 necesidad.AsignacionCoberturaId,
                 necesidad.Estado,
                 necesidad.CerradoPor,
                 necesidad.CerradoUtc
             });

        await _context.SaveChangesAsync();

        return new CoverageResponse
        {
            NecesidadCoberturaId = necesidad.Id,
            AsignacionOrigenId =
                necesidad.AsignacionOrigenId,
            AsignacionCoberturaId =
                necesidad.AsignacionCoberturaId,
            Estado = necesidad.Estado,
            CreadoUtc = necesidad.CreadoUtc
        };
    }
}