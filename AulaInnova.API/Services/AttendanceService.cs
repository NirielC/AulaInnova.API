using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Attendance;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AulaInnova.API.Services;

public class AttendanceService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuthorizationService _authorizationService;

    public AttendanceService(
    AulaInnovaDbContext db,
    AuthorizationService authorizationService)
    {
        _db = db;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Asistencia leída por una estación en modo ASISTENCIA.
    /// Si algo no se cumple lanza RechazoEstacionException con un código
    /// (CREDENCIAL_NO_REGISTRADA, NO_ES_ESTUDIANTE, SIN_JORNADA...).
    /// Pasar la tarjeta dos veces el mismo día no duplica: devuelve la
    /// asistencia existente con YaRegistrada = true.
    /// </summary>
    public async Task<AttendanceResponse> RegisterAsync(
        AttendanceRequest request)
    {
        var ocurridoUtc = EstacionReglas.HoraDeLectura(request.OcurridoUtc);

        var configuracion = await EstacionReglas.ConfiguracionVigenteAsync(
            _db, request.EstacionId, ocurridoUtc);

        await EstacionReglas.ExigirOperacionAsync(
            _db, configuracion.Id, "ASISTENCIA");

        var credencial = await EstacionReglas.CredencialAsync(
            _db, request.ReferenciaNfc, ocurridoUtc);

        var estudiante = await EstacionReglas.EstudianteAsync(
            _db, credencial.PersonaId);

        var jornada = await EstacionReglas.JornadaObligatoriaAsync(
            _db, ocurridoUtc);

        var matricula = await EstacionReglas.MatriculaAsync(
            _db, estudiante.PersonaId, jornada);

        var existing = await BuscarAsistenciaAsync(
            estudiante.PersonaId, jornada.Id);

        if (existing is not null)
            return existing;

        var calidadHora = EstacionReglas.CalidadHora(request.OcurridoUtc);

        var puntualidad = calidadHora == "FIABLE"
            ? ocurridoUtc <= jornada.LimitePuntualUtc
                ? "PUNTUAL"
                : "TARDIA"
            : "PENDIENTE";

        var evento = new Evento
        {
            Id = Guid.NewGuid(),
            HashSolicitud = EstacionReglas.Hash(
                $"{credencial.Referencia}|{request.EstacionId}|{ocurridoUtc:O}"),
            Origen = "ESTACION",
            Accion = "ASISTENCIA",
            PersonaId = estudiante.PersonaId,
            CredencialId = credencial.Id,
            EstacionId = request.EstacionId,
            EstacionConfiguracionId = configuracion.Id,
            OperadorId = null,
            AulaId = configuracion.AulaId,
            JornadaId = jornada.Id,
            MatriculaId = matricula.Id,
            AsignacionId = null,
            PuntoManual = null,
            MotivoCodigo = null,
            MotivoManual = null,
            OcurridoUtc = ocurridoUtc,
            RecibidoUtc = DateTime.UtcNow,
            CalidadHora = calidadHora,
            Estado = "ACEPTADO",
            CodigoResultado = "ASISTENCIA_REGISTRADA"
        };

        var asistencia = new Asistencia
        {
            EstudianteId = estudiante.PersonaId,
            JornadaId = jornada.Id,
            MatriculaId = matricula.Id,
            EventoLlegadaId = evento.Id,
            LlegadaUtc = ocurridoUtc,
            CalidadHora = calidadHora,
            Estado = "PRESENTE",
            Puntualidad = puntualidad,
            CreadoUtc = DateTime.UtcNow
        };

        _db.Eventos.Add(evento);
        _db.Asistencias.Add(asistencia);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Dos lecturas casi simultáneas (por ejemplo la cola offline y
            // una lectura en vivo): UQ_asistencia_estudiante_jornada deja
            // pasar solo una. La otra devuelve la que quedó guardada.
            _db.ChangeTracker.Clear();

            return await BuscarAsistenciaAsync(
                       estudiante.PersonaId, jornada.Id)
                   ?? throw new InvalidOperationException(
                       "No se pudo guardar la asistencia.");
        }

        return new AttendanceResponse
        {
            AsistenciaId = asistencia.Id,
            Estado = asistencia.Estado,
            Puntualidad = asistencia.Puntualidad,
            LlegadaUtc = asistencia.LlegadaUtc,
            YaRegistrada = false
        };
    }

    private async Task<AttendanceResponse?> BuscarAsistenciaAsync(
        long estudianteId,
        long jornadaId)
    {
        return await _db.Asistencias
            .AsNoTracking()
            .Where(a =>
                a.EstudianteId == estudianteId &&
                a.JornadaId == jornadaId)
            .Select(a => new AttendanceResponse
            {
                AsistenciaId = a.Id,
                Estado = a.Estado,
                Puntualidad = a.Puntualidad,
                LlegadaUtc = a.LlegadaUtc,
                YaRegistrada = true
            })
            .FirstOrDefaultAsync();
    }

    public async Task<ManualAttendanceResponse> RegisterManualAsync(
        ManualAttendanceRequest request,
        ClaimsPrincipal user)
    {
        if (request.EstudianteId <= 0)
            throw new ArgumentException(
                "El estudiante es obligatorio.");

        if (string.IsNullOrWhiteSpace(request.PuntoManual))
            throw new ArgumentException(
                "El punto manual es obligatorio.");

        if (string.IsNullOrWhiteSpace(request.MotivoManual))
            throw new ArgumentException(
                "El motivo manual es obligatorio.");

        var estudiante = await _db.Estudiantes
            .Include(e => e.Persona)
            .FirstOrDefaultAsync(e =>
                e.PersonaId == request.EstudianteId);

        if (estudiante is null ||
            !estudiante.Persona.Habilitada)
        {
            throw new ArgumentException(
                "El estudiante no existe o está deshabilitado.");
        }

        var autorizado =
            await _authorizationService.CanAccessStudentAsync(
                user,
                "ASISTENCIA_REGISTRAR",
                request.EstudianteId);

        if (!autorizado)
        {
            throw new UnauthorizedAccessException(
                "El usuario no está autorizado para registrar asistencia de este estudiante.");
        }

        var operadorClaim = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(operadorClaim, out var operadorId))
        {
            throw new UnauthorizedAccessException(
                "No se pudo identificar al usuario autenticado.");
        }


        var ocurridoUtc = request.OcurridoUtc ?? DateTime.UtcNow;

        if (ocurridoUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException(
                "OcurridoUtc debe estar expresado en UTC.");

        Jornada? jornada;

        if (request.JornadaId.HasValue)
        {
            jornada = await _db.Jornadas
                .FirstOrDefaultAsync(j =>
                    j.Id == request.JornadaId.Value);

            if (jornada is null)
                throw new KeyNotFoundException(
                    "La jornada indicada no existe.");
        }
        else
        {
            jornada = await _db.Jornadas
                .FirstOrDefaultAsync(j =>
                    j.InicioUtc <= ocurridoUtc &&
                    j.FinUtc >= ocurridoUtc &&
                    j.Estado != "CANCELADA");
        }

        if (jornada is null)
            throw new InvalidOperationException(
                "No existe una jornada válida para el momento indicado.");

        var matricula = await _db.Matriculas
            .FirstOrDefaultAsync(m =>
                m.EstudianteId == request.EstudianteId &&
                !m.Anulada &&
                m.Desde <= jornada.FechaLocal &&
                (m.Hasta == null ||
                 m.Hasta >= jornada.FechaLocal));

        if (matricula is null)
            throw new InvalidOperationException(
                "El estudiante no tiene una matrícula válida para la jornada.");

        var existing = await _db.Asistencias
            .FirstOrDefaultAsync(a =>
                a.EstudianteId == request.EstudianteId &&
                a.JornadaId == jornada.Id);

        if (existing is not null)
        {
            return new ManualAttendanceResponse
            {
                AsistenciaId = existing.Id,
                EventoId = existing.EventoLlegadaId!.Value,
                Estado = existing.Estado,
                Puntualidad = existing.Puntualidad,
                LlegadaUtc = existing.LlegadaUtc!.Value
            };
        }

        var calidadHora = request.OcurridoUtc.HasValue
            ? "DECLARADA"
            : "FIABLE";

        var puntualidad = calidadHora == "FIABLE"
            ? ocurridoUtc <= jornada.LimitePuntualUtc
                ? "PUNTUAL"
                : "TARDIA"
            : "PENDIENTE";

        var evento = new Evento
        {
            Id = Guid.NewGuid(),
            HashSolicitud = SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    $"{operadorId}|{request.EstudianteId}|{jornada.Id}|{ocurridoUtc:O}|{request.PuntoManual}|{request.MotivoManual}")),
            Origen = "MANUAL",
            Accion = "ASISTENCIA",
            PersonaId = request.EstudianteId,
            CredencialId = null,
            EstacionId = null,
            EstacionConfiguracionId = null,
            OperadorId = operadorId,
            AulaId = null,
            JornadaId = jornada.Id,
            MatriculaId = matricula.Id,
            AsignacionId = null,
            PuntoManual = request.PuntoManual.Trim(),
            MotivoCodigo = null,
            MotivoManual = request.MotivoManual.Trim(),
            OcurridoUtc = ocurridoUtc,
            RecibidoUtc = DateTime.UtcNow,
            CalidadHora = calidadHora,
            Estado = "ACEPTADO",
            CodigoResultado = "ASISTENCIA_MANUAL"
        };

        var asistencia = new Asistencia
        {
            EstudianteId = request.EstudianteId,
            JornadaId = jornada.Id,
            MatriculaId = matricula.Id,
            EventoLlegadaId = evento.Id,
            LlegadaUtc = ocurridoUtc,
            CalidadHora = calidadHora,
            Estado = "PRESENTE",
            Puntualidad = puntualidad,
            CreadoUtc = DateTime.UtcNow
        };

        _db.Eventos.Add(evento);
        _db.Asistencias.Add(asistencia);

        await _db.SaveChangesAsync();

        return new ManualAttendanceResponse
        {
            AsistenciaId = asistencia.Id,
            EventoId = evento.Id,
            Estado = asistencia.Estado,
            Puntualidad = asistencia.Puntualidad,
            LlegadaUtc = asistencia.LlegadaUtc!.Value
        };
    }
}