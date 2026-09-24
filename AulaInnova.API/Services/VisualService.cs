using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Visual;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace AulaInnova.API.Services;

public class VisualService
{
    private readonly AulaInnovaDbContext _context;
    private readonly AuthorizationService _authorizationService;
    private readonly AuditService _auditService;

    public VisualService(
        AulaInnovaDbContext context,
        AuthorizationService authorizationService,
        AuditService auditService)
    {
        _context = context;
        _authorizationService = authorizationService;
        _auditService = auditService;
    }

    public async Task<VisualResponse?> CreateContextAsync(
        CreateClassContextRequest request,
        ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return null;

        if (!IsUtc(request.InicioUtc) ||
            !IsUtc(request.FinUtc))
            return null;

        if (string.IsNullOrWhiteSpace(request.Descripcion))
            return null;

        if (request.FinUtc <= request.InicioUtc)
            return null;

        var asignacion = await _context.Asignaciones
            .FirstOrDefaultAsync(a =>
                a.Id == request.AsignacionId);

        if (asignacion is null)
            return null;

        var autorizado =
            await _authorizationService.CanAccessAssignmentAsync(
                user,
                "CONTEXTO_EDITAR",
                asignacion.Id);

        if (!autorizado)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para editar el contexto de esta asignación.");
        }

        if (asignacion.Tipo != "CLASE")
            return null;

        if (request.InicioUtc < asignacion.InicioUtc ||
            request.FinUtc > asignacion.FinUtc)
        {
            return null;
        }

        var contexto = new ContextoClase
        {
            AsignacionId = asignacion.Id,
            AutorId = userId,
            InicioUtc = request.InicioUtc,
            FinUtc = request.FinUtc,
            Descripcion = request.Descripcion.Trim(),
            CreadoUtc = DateTime.UtcNow
        };

        _context.ContextosClase.Add(contexto);

        _auditService.Registrar(
            user,
            "ContextoClase",
            "NUEVA",
            "CREAR",
            "Creación de contexto visual de clase.",
            antes: null,
            despues: new
            {
                contexto.AsignacionId,
                contexto.AutorId,
                contexto.InicioUtc,
                contexto.FinUtc,
                contexto.Descripcion,
                contexto.CreadoUtc
            });

        await _context.SaveChangesAsync();

        return new VisualResponse
        {
            Id = contexto.Id,
            Estado = "CREADO"
        };
    }

    public async Task<VisualResponse?> CreateZoneAsync(
        CreateVisualZoneRequest request,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
            return null;

        if (request.Revision <= 0)
            return null;

        if (!IsValidJson(request.GeometriaJson))
            return null;

        var aulaExists = await _context.Aulas
            .AnyAsync(a => a.Id == request.AulaId);

        if (!aulaExists)
            return null;

        var autorizado =
            await _authorizationService.HasPermissionAsync(
                user,
                "VISION_CONFIGURAR");

        if (!autorizado)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para configurar zonas visuales.");
        }

        var existingActiveZone = await _context.ZonasVisuales
            .FirstOrDefaultAsync(z =>
                z.AulaId == request.AulaId &&
                z.Codigo == request.Codigo &&
                z.Habilitada);

        if (existingActiveZone is not null)
            return null;

        var zone = new ZonaVisual
        {
            AulaId = request.AulaId,
            Codigo = request.Codigo.Trim(),
            Revision = request.Revision,
            GeometriaJson = request.GeometriaJson,
            Habilitada = true
        };

        _context.ZonasVisuales.Add(zone);

        _auditService.Registrar(
            user,
            "ZonaVisual",
            "NUEVA",
            "CREAR",
            "Creación de zona visual.",
            antes: null,
            despues: new
            {
                zone.AulaId,
                zone.Codigo,
                zone.Revision,
                zone.GeometriaJson,
                zone.Habilitada
            });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Otra solicitud creó la zona activa con el mismo código
            // entre nuestra verificación y este guardado
            // (UX_zona_visual_habilitada).
            throw new Exceptions.ConcurrentConflictException(
                "Ya existe una zona habilitada con ese código para esta aula.");
        }

        return new VisualResponse
        {
            Id = zone.Id,
            Estado = "HABILITADA"
        };
    }

    public async Task<VisualResponse?> CreateIndicatorAsync(
        CreateVisualIndicatorRequest request,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo) ||
            string.IsNullOrWhiteSpace(request.Unidad) ||
            string.IsNullOrWhiteSpace(request.Metodo) ||
            string.IsNullOrWhiteSpace(request.ReferenciaValidacion))
        {
            return null;
        }

        if (request.Revision <= 0)
            return null;

        if (!IsValidJson(request.ConfiguracionJson))
            return null;

        var autorizado =
            await _authorizationService.HasPermissionAsync(
                user,
                "VISION_CONFIGURAR");

        if (!autorizado)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para configurar indicadores visuales.");
        }

        var existing = await _context.IndicadoresVisuales
            .FirstOrDefaultAsync(i =>
                i.Codigo == request.Codigo &&
                i.Revision == request.Revision);

        if (existing is not null)
            return null;

        var indicator = new IndicadorVisual
        {
            Codigo = request.Codigo.Trim(),
            Revision = request.Revision,
            Unidad = request.Unidad.Trim(),
            Metodo = request.Metodo.Trim(),
            ConfiguracionJson = request.ConfiguracionJson,
            ReferenciaValidacion =
                request.ReferenciaValidacion.Trim(),
            Habilitado = true
        };

        _context.IndicadoresVisuales.Add(indicator);

        _auditService.Registrar(
            user,
            "IndicadorVisual",
            "NUEVA",
            "CREAR",
            "Creación de indicador visual.",
            antes: null,
            despues: new
            {
                indicator.Codigo,
                indicator.Revision,
                indicator.Unidad,
                indicator.Metodo,
                indicator.ConfiguracionJson,
                indicator.ReferenciaValidacion,
                indicator.Habilitado
            });

        await _context.SaveChangesAsync();

        return new VisualResponse
        {
            Id = indicator.Id,
            Estado = "HABILITADO"
        };
    }

    public async Task<VisualResponse?> OpenSessionAsync(
        OpenVisualSessionRequest request,
        ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return null;

        if (!IsUtc(request.InicioUtc))
            return null;

        if (string.IsNullOrWhiteSpace(
                request.ReferenciaAutorizacion) ||
            string.IsNullOrWhiteSpace(
                request.VersionProceso))
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
                "VISION_CONFIGURAR",
                asignacion.Id);

        if (!autorizado)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para abrir una sesión visual para esta asignación.");
        }

        if (asignacion.Tipo != "CLASE")
            return null;

        if (request.AulaId != asignacion.AulaId)
            return null;

        if (request.InicioUtc < asignacion.InicioUtc ||
            request.InicioUtc > asignacion.FinUtc)
        {
            return null;
        }

        var aulaExists = await _context.Aulas
            .AnyAsync(a => a.Id == request.AulaId);

        if (!aulaExists)
            return null;

        var activeSession = await _context.SesionesVisuales
            .AnyAsync(s =>
                s.AulaId == request.AulaId &&
                s.Estado == "ACTIVA");

        if (activeSession)
            return null;

        var session = new SesionVisual
        {
            AsignacionId = asignacion.Id,
            AulaId = request.AulaId,
            AutorizadaPor = userId,
            ReferenciaAutorizacion =
                request.ReferenciaAutorizacion.Trim(),
            InicioUtc = request.InicioUtc,
            FinUtc = null,
            VersionProceso = request.VersionProceso.Trim(),
            Estado = "ACTIVA"
        };

        _context.SesionesVisuales.Add(session);

        _auditService.Registrar(
            user,
            "SesionVisual",
            "NUEVA",
            "ABRIR",
            "Apertura de sesión visual.",
            antes: null,
            despues: new
            {
                session.AsignacionId,
                session.AulaId,
                session.AutorizadaPor,
                session.ReferenciaAutorizacion,
                session.InicioUtc,
                session.VersionProceso,
                session.Estado
            });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Otra solicitud abrió una sesión activa en la misma aula
            // entre nuestra verificación y este guardado
            // (UX_sesion_visual_aula_activa).
            throw new Exceptions.ConcurrentConflictException(
                "Ya existe una sesión visual activa para esta aula.");
        }

        return new VisualResponse
        {
            Id = session.Id,
            Estado = session.Estado
        };
    }

    public async Task<VisualResponse?> RecordMeasurementAsync(
        RecordVisualMeasurementRequest request)
    {
        if (!IsUtc(request.InicioUtc) ||
            !IsUtc(request.FinUtc))
            return null;

        if (request.FinUtc <= request.InicioUtc)
            return null;

        if (request.MuestrasTotales < 0 ||
            request.MuestrasValidas < 0 ||
            request.MuestrasValidas >
            request.MuestrasTotales)
        {
            return null;
        }

        var session = await _context.SesionesVisuales
            .FirstOrDefaultAsync(s =>
                s.Id == request.SesionId &&
                s.Estado == "ACTIVA");

        if (session is null)
            return null;

        if (request.InicioUtc < session.InicioUtc ||
            request.FinUtc > DateTime.UtcNow ||
            (session.FinUtc.HasValue &&
             request.FinUtc > session.FinUtc.Value))
        {
            return null;
        }

        var zone = await _context.ZonasVisuales
            .FirstOrDefaultAsync(z =>
                z.Id == request.ZonaId &&
                z.AulaId == session.AulaId);

        if (zone is null)
            return null;

        var indicator = await _context.IndicadoresVisuales
            .FirstOrDefaultAsync(i =>
                i.Id == request.IndicadorId &&
                i.Habilitado);

        if (indicator is null)
            return null;

        if (request.Valida)
        {
            if (request.Valor is null ||
                request.Valor < 0 ||
                request.MuestrasValidas <= 0 ||
                !string.IsNullOrWhiteSpace(
                    request.MotivoInvalidez))
            {
                return null;
            }
        }
        else
        {
            if (request.Valor is not null ||
                string.IsNullOrWhiteSpace(
                    request.MotivoInvalidez))
            {
                return null;
            }
        }

        var duplicate = await _context.MedicionesVisuales
            .AnyAsync(m =>
                m.SesionId == request.SesionId &&
                m.ZonaId == request.ZonaId &&
                m.IndicadorId == request.IndicadorId &&
                m.InicioUtc == request.InicioUtc &&
                m.FinUtc == request.FinUtc);

        if (duplicate)
            return null;

        var measurement = new MedicionVisual
        {
            SesionId = session.Id,
            ZonaId = zone.Id,
            IndicadorId = indicator.Id,
            InicioUtc = request.InicioUtc,
            FinUtc = request.FinUtc,
            Valor = request.Valida
                ? request.Valor
                : null,
            Valida = request.Valida,
            MotivoInvalidez = request.Valida
                ? null
                : request.MotivoInvalidez?.Trim(),
            MuestrasValidas = request.MuestrasValidas,
            MuestrasTotales = request.MuestrasTotales,
            CreadoUtc = DateTime.UtcNow
        };

        _context.MedicionesVisuales.Add(measurement);

        await _context.SaveChangesAsync();

        return new VisualResponse
        {
            Id = measurement.Id,
            Estado = request.Valida
                ? "VALIDA"
                : "INVALIDA"
        };
    }

    public async Task<VisualResponse?> CloseSessionAsync(
        long sessionId,
        ClaimsPrincipal user)
    {
        var session = await _context.SesionesVisuales
            .FirstOrDefaultAsync(s =>
                s.Id == sessionId &&
                s.Estado == "ACTIVA");

        if (session is null)
            return null;

        var autorizado =
            await _authorizationService.CanAccessAssignmentAsync(
                user,
                "VISION_CONFIGURAR",
                session.AsignacionId);

        if (!autorizado)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para cerrar esta sesión visual.");
        }

        var antes = new
        {
            session.Id,
            session.AsignacionId,
            session.AulaId,
            session.InicioUtc,
            session.FinUtc,
            session.VersionProceso,
            session.Estado
        };

        session.FinUtc = DateTime.UtcNow;
        session.Estado = "CERRADA";

        _auditService.Registrar(
            user,
            "SesionVisual",
            session.Id.ToString(),
            "CERRAR",
            "Cierre de sesión visual.",
            antes: antes,
            despues: new
            {
                session.Id,
                session.AsignacionId,
                session.AulaId,
                session.InicioUtc,
                session.FinUtc,
                session.VersionProceso,
                session.Estado
            });

        await _context.SaveChangesAsync();

        return new VisualResponse
        {
            Id = session.Id,
            Estado = session.Estado
        };
    }

    /// <summary>
    /// Consulta de mediciones visuales agregadas (permiso VISION_LEER).
    /// Solo devuelve lo que ya define ai.medicion_visual: conteos e
    /// indicadores por zona y ventana, nunca imagen, video ni identidad.
    /// </summary>
    public async Task<List<VisualMeasurementResponse>?>
        GetMeasurementsAsync(
            long sesionId,
            long? zonaId,
            long? indicadorId,
            ClaimsPrincipal user)
    {
        var session = await _context.SesionesVisuales
            .FirstOrDefaultAsync(s => s.Id == sesionId);

        if (session is null)
            return null;

        var autorizado =
            await _authorizationService.CanAccessAssignmentAsync(
                user,
                "VISION_LEER",
                session.AsignacionId);

        if (!autorizado)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para consultar los indicadores visuales de esta asignación.");
        }

        var query = _context.MedicionesVisuales
            .AsNoTracking()
            .Include(m => m.Zona)
            .Include(m => m.Indicador)
            .Where(m => m.SesionId == sesionId);

        if (zonaId.HasValue)
            query = query.Where(m => m.ZonaId == zonaId.Value);

        if (indicadorId.HasValue)
            query = query.Where(m => m.IndicadorId == indicadorId.Value);

        return await query
            .OrderBy(m => m.InicioUtc)
            .Select(m => new VisualMeasurementResponse
            {
                Id = m.Id,
                SesionId = m.SesionId,
                ZonaId = m.ZonaId,
                ZonaCodigo = m.Zona.Codigo,
                IndicadorId = m.IndicadorId,
                IndicadorCodigo = m.Indicador.Codigo,
                IndicadorUnidad = m.Indicador.Unidad,
                InicioUtc = m.InicioUtc,
                FinUtc = m.FinUtc,
                Valor = m.Valor,
                Valida = m.Valida,
                MotivoInvalidez = m.MotivoInvalidez,
                MuestrasValidas = m.MuestrasValidas,
                MuestrasTotales = m.MuestrasTotales
            })
            .ToListAsync();
    }

    private static bool IsUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc;
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var document =
                JsonDocument.Parse(json);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}