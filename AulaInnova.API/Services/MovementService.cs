using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Movements;
using AulaInnova.API.Models;
using AulaInnova.API.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AulaInnova.API.Services;

/// <summary>
/// Salidas temporales del aula (baño, clínica...) y regresos, leídos por
/// una estación en modo MOVIMIENTOS. Cada regla que no se cumple lanza
/// RechazoEstacionException con un código para la pantalla de la estación.
/// </summary>
public class MovementService
{
    // Tiempo de referencia para considerar "vencida" una salida
    // (salida_temporal.expira_utc). La salida sigue ABIERTA hasta que el
    // estudiante regrese; si la jornada termina sin regreso, pasa a EXPIRADA.
    private static readonly TimeSpan DuracionMaximaSalida = TimeSpan.FromMinutes(15);

    private readonly AulaInnovaDbContext _context;

    public MovementService(AulaInnovaDbContext context)
    {
        _context = context;
    }

    public async Task<MovementResponse> RegisterExitAsync(
        MovementExitRequest request)
    {
        var (seleccionId, seleccionEdadMs) = EstacionReglas.Seleccion(
            request.SeleccionId, request.SeleccionEdadMs);

        var repetida = await RespuestaRepetidaAsync(
            request.EstacionId, seleccionId, "SALIDA_AULA");

        if (repetida is not null)
            return repetida;

        var utcNow = EstacionReglas.HoraDeLectura(request.OcurridoUtc);

        var configuracion = await EstacionReglas.ConfiguracionVigenteAsync(
            _context, request.EstacionId, utcNow);

        await EstacionReglas.ExigirOperacionAsync(
            _context, configuracion.Id, "SALIDA_AULA");

        if (configuracion.AulaId is null)
            throw new RechazoEstacionException(
                "ESTACION_SIN_AULA",
                "Las salidas solo se registran en una estación de aula.");

        var motivo = await _context.MotivosSalida
            .FirstOrDefaultAsync(m =>
                m.Codigo == request.MotivoCodigo &&
                m.Habilitado);

        if (motivo is null)
            throw new RechazoEstacionException(
                "MOTIVO_INVALIDO",
                "El motivo de salida no existe o está deshabilitado.");

        var credencial = await EstacionReglas.CredencialAsync(
            _context, request.ReferenciaNfc, utcNow);

        var estudiante = await EstacionReglas.EstudianteAsync(
            _context, credencial.PersonaId);

        var jornada = await EstacionReglas.JornadaObligatoriaAsync(
            _context, utcNow);

        var matricula = await EstacionReglas.MatriculaAsync(
            _context, estudiante.PersonaId, jornada);

        await ExpirarSalidasDeJornadasTerminadasAsync(
            estudiante.PersonaId, utcNow);

        var salidaAbierta = await _context.SalidasTemporales
            .AnyAsync(s =>
                s.EstudianteId == estudiante.PersonaId &&
                s.JornadaId == jornada.Id &&
                s.Estado == "ABIERTA");

        if (salidaAbierta)
            throw new RechazoEstacionException(
                "SALIDA_YA_ABIERTA",
                "El estudiante ya está fuera: primero debe registrar su regreso.");

        var calidadHora = EstacionReglas.CalidadHora(request.OcurridoUtc);

        var evento = new Evento
        {
            Id = Guid.NewGuid(),
            HashSolicitud = EstacionReglas.Hash(
                $"{credencial.Referencia}|{request.EstacionId}|SALIDA_AULA|{utcNow:O}"),
            Origen = "ESTACION",
            Accion = "SALIDA_AULA",
            PersonaId = estudiante.PersonaId,
            CredencialId = credencial.Id,
            EstacionId = request.EstacionId,
            EstacionConfiguracionId = configuracion.Id,
            AulaId = configuracion.AulaId,
            JornadaId = jornada.Id,
            MatriculaId = matricula.Id,
            MotivoCodigo = motivo.Codigo,
            OcurridoUtc = utcNow,
            RecibidoUtc = DateTime.UtcNow,
            CalidadHora = calidadHora,
            Estado = "ACEPTADO",
            CodigoResultado = "SALIDA_REGISTRADA",
            // CK_evento_seleccion_estacion: toda acción de estación que no
            // sea ASISTENCIA lleva seleccion_id y seleccion_edad_ms.
            SeleccionId = seleccionId,
            SeleccionEdadMs = seleccionEdadMs
        };

        _context.Eventos.Add(evento);

        var salida = new SalidaTemporal
        {
            EstudianteId = estudiante.PersonaId,
            JornadaId = jornada.Id,
            MatriculaId = matricula.Id,
            AulaId = configuracion.AulaId.Value,
            MotivoCodigo = motivo.Codigo,
            EventoSalidaId = evento.Id,
            SalidaUtc = utcNow,
            ExpiraUtc = utcNow + DuracionMaximaSalida,
            // CK_salida_temporal_horas_fiables: solo una salida CERRADA
            // puede tener horas fiables; se decide al registrar el regreso.
            HorasFiables = false,
            Estado = "ABIERTA",
            CreadoUtc = DateTime.UtcNow
        };

        _context.SalidasTemporales.Add(salida);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // La misma selección llegó dos veces a la vez
            // (UX_evento_estacion_seleccion).
            _context.ChangeTracker.Clear();

            return await RespuestaRepetidaAsync(
                       request.EstacionId, seleccionId, "SALIDA_AULA")
                   ?? throw new InvalidOperationException(
                       "No se pudo guardar la salida.");
        }

        return ToResponse(salida, "SALIDA_AULA", repetida: false);
    }

    public async Task<MovementResponse> RegisterReturnAsync(
        MovementReturnRequest request)
    {
        var (seleccionId, seleccionEdadMs) = EstacionReglas.Seleccion(
            request.SeleccionId, request.SeleccionEdadMs);

        var repetida = await RespuestaRepetidaAsync(
            request.EstacionId, seleccionId, "REGRESO_AULA");

        if (repetida is not null)
            return repetida;

        var utcNow = EstacionReglas.HoraDeLectura(request.OcurridoUtc);

        var configuracion = await EstacionReglas.ConfiguracionVigenteAsync(
            _context, request.EstacionId, utcNow);

        await EstacionReglas.ExigirOperacionAsync(
            _context, configuracion.Id, "REGRESO_AULA");

        var credencial = await EstacionReglas.CredencialAsync(
            _context, request.ReferenciaNfc, utcNow);

        var estudiante = await EstacionReglas.EstudianteAsync(
            _context, credencial.PersonaId);

        var jornada = await EstacionReglas.JornadaObligatoriaAsync(
            _context, utcNow);

        await ExpirarSalidasDeJornadasTerminadasAsync(
            estudiante.PersonaId, utcNow);

        var salida = await _context.SalidasTemporales
            .Where(s =>
                s.EstudianteId == estudiante.PersonaId &&
                s.JornadaId == jornada.Id &&
                s.Estado == "ABIERTA")
            .OrderByDescending(s => s.SalidaUtc)
            .FirstOrDefaultAsync();

        if (salida is null)
            throw new RechazoEstacionException(
                "SIN_SALIDA_ABIERTA",
                "El estudiante no tiene una salida abierta.");

        if (salida.SalidaUtc.HasValue &&
            utcNow < salida.SalidaUtc.Value)
        {
            throw new RechazoEstacionException(
                "REGRESO_ANTES_DE_SALIDA",
                "La hora del regreso es anterior a la de la salida.");
        }

        var calidadHora = EstacionReglas.CalidadHora(request.OcurridoUtc);

        var evento = new Evento
        {
            Id = Guid.NewGuid(),
            HashSolicitud = EstacionReglas.Hash(
                $"{credencial.Referencia}|{request.EstacionId}|REGRESO_AULA|{utcNow:O}"),
            Origen = "ESTACION",
            Accion = "REGRESO_AULA",
            PersonaId = estudiante.PersonaId,
            CredencialId = credencial.Id,
            EstacionId = request.EstacionId,
            EstacionConfiguracionId = configuracion.Id,
            AulaId = salida.AulaId,
            JornadaId = salida.JornadaId,
            MatriculaId = salida.MatriculaId,
            OcurridoUtc = utcNow,
            RecibidoUtc = DateTime.UtcNow,
            CalidadHora = calidadHora,
            Estado = "ACEPTADO",
            CodigoResultado = "REGRESO_REGISTRADO",
            SeleccionId = seleccionId,
            SeleccionEdadMs = seleccionEdadMs
        };

        _context.Eventos.Add(evento);

        // La duración fuera del aula solo es fiable si la salida y el
        // regreso tienen la hora del servidor (no registros offline).
        var salidaFiable = await _context.Eventos
            .Where(e => e.Id == salida.EventoSalidaId)
            .Select(e => e.CalidadHora)
            .FirstOrDefaultAsync() == "FIABLE";

        salida.EventoRegresoId = evento.Id;
        salida.RegresoUtc = utcNow;
        salida.Estado = "CERRADA";
        salida.HorasFiables = salidaFiable && calidadHora == "FIABLE";

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();

            return await RespuestaRepetidaAsync(
                       request.EstacionId, seleccionId, "REGRESO_AULA")
                   ?? throw new InvalidOperationException(
                       "No se pudo guardar el regreso.");
        }

        return ToResponse(salida, "REGRESO_AULA", repetida: false);
    }

    /// <summary>
    /// Si la estación reenvía una selección que ya se guardó (cola
    /// offline, o se cortó la red justo después de guardar), devuelve el
    /// resultado original en vez de rechazarla o duplicarla.
    /// </summary>
    private async Task<MovementResponse?> RespuestaRepetidaAsync(
        long estacionId,
        Guid seleccionId,
        string accion)
    {
        var evento = await EstacionReglas.EventoPorSeleccionAsync(
            _context, estacionId, seleccionId);

        if (evento is null)
            return null;

        var salida = evento.Accion == accion
            ? await _context.SalidasTemporales
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.EventoSalidaId == evento.Id ||
                    s.EventoRegresoId == evento.Id)
            : null;

        if (salida is null)
            throw new RechazoEstacionException(
                "SELECCION_REPETIDA",
                "Ese seleccionId ya se usó para otra operación.");

        return ToResponse(salida, accion, repetida: true);
    }

    /// <summary>
    /// Una salida que quedó ABIERTA en una jornada que ya terminó no puede
    /// cerrarse con un regreso de otro día: pasa a EXPIRADA para no
    /// bloquear al estudiante (CK_salida_temporal_estado_contexto exige
    /// expira_utc en ese estado).
    /// </summary>
    private async Task ExpirarSalidasDeJornadasTerminadasAsync(
        long estudianteId,
        DateTime utcNow)
    {
        var vencidas = await _context.SalidasTemporales
            .Where(s =>
                s.EstudianteId == estudianteId &&
                s.Estado == "ABIERTA" &&
                _context.Jornadas.Any(j =>
                    j.Id == s.JornadaId &&
                    j.FinUtc < utcNow))
            .ToListAsync();

        foreach (var salida in vencidas)
        {
            salida.Estado = "EXPIRADA";
            salida.ExpiraUtc ??= salida.SalidaUtc ?? salida.CreadoUtc;
        }
    }

    private static MovementResponse ToResponse(
        SalidaTemporal salida,
        string accion,
        bool repetida)
    {
        return new MovementResponse
        {
            SalidaTemporalId = salida.Id,
            Accion = accion,
            Estado = salida.Estado,
            MotivoCodigo = salida.MotivoCodigo,
            SalidaUtc = salida.SalidaUtc,
            RegresoUtc = salida.RegresoUtc,
            ExpiraUtc = salida.ExpiraUtc,
            Repetida = repetida
        };
    }
}
