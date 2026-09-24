using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Incidents;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AulaInnova.API.Services;

public class IncidentService
{
    private readonly AulaInnovaDbContext _context;
    private readonly AuthorizationService _authorizationService;

    public IncidentService(
        AulaInnovaDbContext context,
        AuthorizationService authorizationService)
    {
        _context = context;
        _authorizationService = authorizationService;
    }

    public async Task<IncidentResponse?> CreateAsync(
        CreateIncidentRequest request,
        ClaimsPrincipal user)
    {
        // 1. Obtener usuario autenticado
        var userIdClaim = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return null;

        // 2. Validar estudiante
        var estudiante = await _context.Estudiantes
            .FirstOrDefaultAsync(e =>
                e.PersonaId == request.EstudianteId);

        if (estudiante is null)
            return null;

        var autorizado =
            await _authorizationService.CanAccessStudentAsync(
                user,
                "CONDUCTA_REGISTRAR",
                request.EstudianteId);

        if (!autorizado)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para registrar una incidencia de este estudiante.");
        }

        // 3. Validar persona habilitada
        var persona = await _context.Personas
            .FirstOrDefaultAsync(p =>
                p.Id == estudiante.PersonaId &&
                p.Habilitada);

        if (persona is null)
            return null;

        // 4. Buscar jornada actual
        var utcNow = DateTime.UtcNow;

        var jornada = await _context.Jornadas
            .FirstOrDefaultAsync(j =>
                j.InicioUtc <= utcNow &&
                j.FinUtc >= utcNow &&
                j.Estado == "ABIERTA");

        if (jornada is null)
            return null;

        // 5. Validar tipo de incidencia habilitado
        var tipo = await _context.TiposIncidencia
            .FirstOrDefaultAsync(t =>
                t.Id == request.TipoIncidenciaId &&
                t.Habilitado);

        if (tipo is null)
            return null;

        // 6. Evitar incidencia vigente duplicada
        var incidenciaExistente =
            await _context.Incidencias
                .FirstOrDefaultAsync(i =>
                    i.EstudianteId == estudiante.PersonaId &&
                    i.JornadaId == jornada.Id &&
                    i.TipoIncidenciaId == tipo.Id &&
                    i.Vigente);

        if (incidenciaExistente is not null)
        {
            return new IncidentResponse
            {
                IncidenciaId = incidenciaExistente.Id,
                EstudianteId = incidenciaExistente.EstudianteId,
                JornadaId = incidenciaExistente.JornadaId,
                TipoIncidenciaId =
                    incidenciaExistente.TipoIncidenciaId,
                Vigente = incidenciaExistente.Vigente,
                CreadoUtc = incidenciaExistente.CreadoUtc
            };
        }

        // 7. Validar motivo manual
        if (string.IsNullOrWhiteSpace(request.Motivo))
            return null;

        // 8. Crear evento de origen manual
        var eventoId = Guid.NewGuid();

        var contenidoHash =
            $"{request.EstudianteId}|{request.TipoIncidenciaId}|REGISTRAR_INCIDENCIA|{utcNow:O}|{userId}";

        var hashSolicitud = SHA256.HashData(
            Encoding.UTF8.GetBytes(contenidoHash));

        var evento = new Evento
        {
            Id = eventoId,
            HashSolicitud = hashSolicitud,
            Origen = "MANUAL",
            Accion = "REGISTRAR_INCIDENCIA",
            PersonaId = estudiante.PersonaId,
            OperadorId = userId,
            JornadaId = jornada.Id,
            OcurridoUtc = utcNow,
            RecibidoUtc = utcNow,
            CalidadHora = "FIABLE",
            Estado = "ACEPTADO",
            CodigoResultado = "INCIDENCIA_REGISTRADA",
            MotivoManual = request.Motivo.Trim()
        };

        _context.Eventos.Add(evento);

        // 9. Crear incidencia
        var incidencia = new Incidencia
        {
            EstudianteId = estudiante.PersonaId,
            JornadaId = jornada.Id,
            TipoIncidenciaId = tipo.Id,
            EventoOrigenId = eventoId,
            CreadaPor = userId,
            Vigente = true,
            CreadoUtc = utcNow
        };

        _context.Incidencias.Add(incidencia);

        await _context.SaveChangesAsync();

        return new IncidentResponse
        {
            IncidenciaId = incidencia.Id,
            EstudianteId = incidencia.EstudianteId,
            JornadaId = incidencia.JornadaId,
            TipoIncidenciaId = incidencia.TipoIncidenciaId,
            Vigente = incidencia.Vigente,
            CreadoUtc = incidencia.CreadoUtc
        };
    }

    public async Task<bool> AddObservationAsync(
        AddIncidentObservationRequest request,
        ClaimsPrincipal user)
    {
        // 1. Usuario autenticado
        var userIdClaim = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return false;

        // 2. Validar incidencia vigente
        var incidencia = await _context.Incidencias
            .FirstOrDefaultAsync(i =>
                i.Id == request.IncidenciaId &&
                i.Vigente);

        if (incidencia is null)
            return false;

        var autorizado =
            await _authorizationService.CanAccessStudentAsync(
                user,
                "CONDUCTA_REGISTRAR",
                incidencia.EstudianteId);

        if (!autorizado)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para ampliar esta incidencia.");
        }

        // 3. Validar texto
        if (string.IsNullOrWhiteSpace(request.Texto))
            return false;

        var utcNow = DateTime.UtcNow;

        // 4. Crear evento de observación
        var eventoId = Guid.NewGuid();

        var contenidoHash =
            $"{request.IncidenciaId}|AMPLIAR_OBSERVACION|{utcNow:O}|{userId}";

        var hashSolicitud = SHA256.HashData(
            Encoding.UTF8.GetBytes(contenidoHash));

        var evento = new Evento
        {
            Id = eventoId,
            HashSolicitud = hashSolicitud,
            Origen = "MANUAL",
            Accion = "AMPLIAR_OBSERVACION",
            PersonaId = incidencia.EstudianteId,
            OperadorId = userId,
            JornadaId = incidencia.JornadaId,
            OcurridoUtc = request.HechoUtc ?? utcNow,
            RecibidoUtc = utcNow,
            CalidadHora = request.HechoUtc.HasValue
                ? "DECLARADA"
                : "FIABLE",
            Estado = "ACEPTADO",
            CodigoResultado = "OBSERVACION_REGISTRADA",
            MotivoManual = "AMPLIAR_OBSERVACION"
        };

        _context.Eventos.Add(evento);

        // 5. Crear observación
        var observacion = new ObservacionIncidencia
        {
            IncidenciaId = incidencia.Id,
            EventoId = eventoId,
            AutorId = userId,
            Texto = request.Texto.Trim(),
            HechoUtc = request.HechoUtc,
            Comunicable = request.Comunicable,
            Vigente = true,
            CreadoUtc = utcNow
        };

        _context.ObservacionesIncidencia.Add(observacion);

        await _context.SaveChangesAsync();

        return true;
    }
}