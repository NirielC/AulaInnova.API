using AulaInnova.API.Data;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AulaInnova.API.Services;

/// <summary>
/// Convierte el horario semanal (ai.horario_clase) en las clases reales de
/// un día (ai.asignacion tipo CLASE). Así la estación y la cámara saben
/// qué clase hay en cada aula a cada hora sin que nadie la cree a mano.
///
/// Es idempotente: un horario que ya tiene asignación en esa jornada (en
/// cualquier estado, incluso CANCELADA) no se vuelve a crear. Se llama al
/// crear una jornada, desde el endpoint de administración y, por si el
/// horario cambió después, cuando una estación pregunta por la clase actual.
/// </summary>
public class AsignacionGeneradorService
{
    private readonly AulaInnovaDbContext _db;
    private readonly ILogger<AsignacionGeneradorService> _logger;

    public AsignacionGeneradorService(
        AulaInnovaDbContext db,
        ILogger<AsignacionGeneradorService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ResultadoGeneracion> GenerarAsync(Jornada jornada)
    {
        var resultado = new ResultadoGeneracion();

        if (jornada.Estado == "CANCELADA")
            return resultado;

        var fecha = jornada.FechaLocal;
        var dia = DiaSemanaIso(fecha.DayOfWeek);

        var horarios = await _db.HorariosClase
            .AsNoTracking()
            .Where(h =>
                !h.Anulado &&
                h.DiaSemana == dia &&
                h.VigenteDesde <= fecha &&
                (h.VigenteHasta == null || h.VigenteHasta >= fecha))
            .ToListAsync();

        if (horarios.Count == 0)
            return resultado;

        var yaGenerados = await _db.Asignaciones
            .Where(a =>
                a.JornadaId == jornada.Id &&
                a.HorarioClaseId != null)
            .Select(a => a.HorarioClaseId!.Value)
            .ToListAsync();

        var activas = await _db.Asignaciones
            .AsNoTracking()
            .Where(a =>
                a.JornadaId == jornada.Id &&
                a.Estado == "ACTIVA")
            .ToListAsync();

        var zona = ZonaHoraria(jornada.ZonaHoraria);

        foreach (var horario in horarios.Where(h => !yaGenerados.Contains(h.Id)))
        {
            var inicioUtc = ConvertirAUtc(fecha, horario.HoraInicio, zona);
            var finUtc = ConvertirAUtc(fecha, horario.HoraFin, zona);

            // Misma regla que AdminAsignacionService: la clase debe caber
            // en la jornada y no cruzarse con otra del mismo grupo,
            // docente o aula.
            if (inicioUtc < jornada.InicioUtc || finUtc > jornada.FinUtc)
            {
                resultado.Omitidas.Add(
                    $"Horario {horario.Id}: fuera del horario de la jornada.");
                continue;
            }

            var cruce = activas.FirstOrDefault(a =>
                (a.GrupoId == horario.GrupoId ||
                 a.PersonalId == horario.DocenteId ||
                 a.AulaId == horario.AulaId) &&
                a.InicioUtc < finUtc &&
                a.FinUtc > inicioUtc);

            if (cruce is not null)
            {
                resultado.Omitidas.Add(
                    $"Horario {horario.Id}: se cruza con la asignación {cruce.Id}.");
                continue;
            }

            var asignacion = new Asignacion
            {
                JornadaId = jornada.Id,
                GrupoId = horario.GrupoId,
                HorarioClaseId = horario.Id,
                PersonalId = horario.DocenteId,
                AulaId = horario.AulaId,
                Tipo = "CLASE",
                InicioUtc = inicioUtc,
                FinUtc = finUtc,
                Estado = "ACTIVA",
                CreadoUtc = DateTime.UtcNow
            };

            _db.Asignaciones.Add(asignacion);
            activas.Add(asignacion);
            resultado.Creadas++;
        }

        if (resultado.Creadas == 0)
            return resultado;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Dos estaciones generaron el mismo día a la vez
            // (UX_asignacion_activa_exacta): la otra ya las guardó.
            _logger.LogWarning(ex,
                "Generación concurrente de asignaciones para la jornada {JornadaId}.",
                jornada.Id);

            _db.ChangeTracker.Clear();
            resultado.Creadas = 0;
        }

        return resultado;
    }

    /// <summary>1 = lunes ... 7 = domingo (CK_horario_clase_dia).</summary>
    public static byte DiaSemanaIso(DayOfWeek dia) =>
        dia == DayOfWeek.Sunday ? (byte)7 : (byte)dia;

    public static DateTime ConvertirAUtc(
        DateOnly fecha,
        TimeOnly hora,
        TimeZoneInfo zona)
    {
        var local = fecha.ToDateTime(hora, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, zona);
    }

    /// <summary>
    /// jornada.zona_horaria guarda un id IANA ("America/El_Salvador").
    /// .NET lo entiende en Windows y Linux; si el sistema no lo tiene, se
    /// usa UTC-6 fijo (El Salvador no tiene horario de verano).
    /// </summary>
    public static TimeZoneInfo ZonaHoraria(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "UTC-06", TimeSpan.FromHours(-6), "UTC-06", "UTC-06");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "UTC-06", TimeSpan.FromHours(-6), "UTC-06", "UTC-06");
        }
    }
}

public class ResultadoGeneracion
{
    public int Creadas { get; set; }
    public List<string> Omitidas { get; set; } = new();
}
