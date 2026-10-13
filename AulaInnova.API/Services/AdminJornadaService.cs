using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin.Jornada;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminJornadaService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;
    private readonly AsignacionGeneradorService _generador;

    // CK_jornada_estado. PROGRAMADA y ACTIVA se aceptan como sinónimos
    // de PLANIFICADA y ABIERTA para no romper clientes que ya los usan.
    private static readonly Dictionary<string, string> Estados =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PLANIFICADA"] = "PLANIFICADA",
            ["PROGRAMADA"] = "PLANIFICADA",
            ["ABIERTA"] = "ABIERTA",
            ["ACTIVA"] = "ABIERTA",
            ["CERRADA"] = "CERRADA",
            ["CANCELADA"] = "CANCELADA"
        };

    public AdminJornadaService(
        AulaInnovaDbContext db,
        AuditService auditService,
        AsignacionGeneradorService generador)
    {
        _db = db;
        _auditService = auditService;
        _generador = generador;
    }

    public async Task<JornadaResponse> CreateAsync(
        CreateJornadaRequest request,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.ZonaHoraria))
            throw new ArgumentException("La zona horaria es obligatoria.");

        if (request.FinUtc <= request.InicioUtc)
            throw new ArgumentException(
                "FinUtc debe ser posterior a InicioUtc.");

        if (request.LimitePuntualUtc < request.InicioUtc ||
            request.LimitePuntualUtc > request.FinUtc)
        {
            throw new ArgumentException(
                "LimitePuntualUtc debe estar dentro del intervalo de la jornada.");
        }

        var exists = await _db.Jornadas
            .AnyAsync(j => j.FechaLocal == request.FechaLocal);

        if (exists)
            throw new InvalidOperationException(
                "Ya existe una jornada para esa fecha.");

        var jornada = new Jornada
        {
            FechaLocal = request.FechaLocal,
            ZonaHoraria = request.ZonaHoraria.Trim(),
            InicioUtc = request.InicioUtc,
            FinUtc = request.FinUtc,
            LimitePuntualUtc = request.LimitePuntualUtc,
            Estado = "PLANIFICADA"
        };

        _db.Jornadas.Add(jornada);

        _auditService.Registrar(
            user,
            "Jornada",
            "NUEVA",
            "CREAR",
            "Creación de jornada.",
            antes: null,
            despues: new
            {
                jornada.FechaLocal,
                jornada.ZonaHoraria,
                jornada.InicioUtc,
                jornada.FinUtc,
                jornada.LimitePuntualUtc,
                jornada.Estado
            });

        await _db.SaveChangesAsync();

        // Las clases del día salen del horario semanal.
        await _generador.GenerarAsync(jornada);

        return ToResponse(jornada);
    }

    /// <summary>
    /// Crea las jornadas de un periodo y sus clases (desde el horario).
    /// </summary>
    public async Task<GeneracionJornadasResponse> CreateRangeAsync(
        CreateJornadasRangoRequest request,
        ClaimsPrincipal user)
    {
        if (request.Hasta < request.Desde)
            throw new ArgumentException(
                "Hasta no puede ser anterior a Desde.");

        if (request.Hasta.DayNumber - request.Desde.DayNumber > 200)
            throw new ArgumentException(
                "El periodo no puede pasar de 200 días.");

        if (request.HoraFin <= request.HoraInicio)
            throw new ArgumentException(
                "HoraFin debe ser posterior a HoraInicio.");

        if (request.HoraLimitePuntual < request.HoraInicio ||
            request.HoraLimitePuntual > request.HoraFin)
            throw new ArgumentException(
                "HoraLimitePuntual debe estar entre HoraInicio y HoraFin.");

        if (request.Dias.Count == 0 ||
            request.Dias.Any(d => d < 1 || d > 7))
            throw new ArgumentException(
                "Dias debe tener valores de 1 (lunes) a 7 (domingo).");

        if (string.IsNullOrWhiteSpace(request.ZonaHoraria))
            throw new ArgumentException("La zona horaria es obligatoria.");

        var zonaId = request.ZonaHoraria.Trim();
        var zona = AsignacionGeneradorService.ZonaHoraria(zonaId);

        var existentes = await _db.Jornadas
            .Where(j =>
                j.FechaLocal >= request.Desde &&
                j.FechaLocal <= request.Hasta)
            .Select(j => j.FechaLocal)
            .ToListAsync();

        var respuesta = new GeneracionJornadasResponse();
        var nuevas = new List<Jornada>();

        for (var fecha = request.Desde; fecha <= request.Hasta; fecha = fecha.AddDays(1))
        {
            if (!request.Dias.Contains(
                    AsignacionGeneradorService.DiaSemanaIso(fecha.DayOfWeek)))
                continue;

            if (existentes.Contains(fecha))
            {
                respuesta.Omitidas.Add($"{fecha:yyyy-MM-dd}: ya tenía jornada.");
                continue;
            }

            var jornada = new Jornada
            {
                FechaLocal = fecha,
                ZonaHoraria = zonaId,
                InicioUtc = AsignacionGeneradorService.ConvertirAUtc(
                    fecha, request.HoraInicio, zona),
                FinUtc = AsignacionGeneradorService.ConvertirAUtc(
                    fecha, request.HoraFin, zona),
                LimitePuntualUtc = AsignacionGeneradorService.ConvertirAUtc(
                    fecha, request.HoraLimitePuntual, zona),
                Estado = "PLANIFICADA"
            };

            _db.Jornadas.Add(jornada);
            nuevas.Add(jornada);

            _auditService.Registrar(
                user,
                "Jornada",
                "NUEVA",
                "CREAR",
                "Creación de jornada por periodo.",
                antes: null,
                despues: new
                {
                    jornada.FechaLocal,
                    jornada.ZonaHoraria,
                    jornada.InicioUtc,
                    jornada.FinUtc,
                    jornada.LimitePuntualUtc,
                    jornada.Estado
                });
        }

        await _db.SaveChangesAsync();

        foreach (var jornada in nuevas)
        {
            var generacion = await _generador.GenerarAsync(jornada);
            respuesta.AsignacionesCreadas += generacion.Creadas;
            respuesta.Omitidas.AddRange(generacion.Omitidas
                .Select(o => $"{jornada.FechaLocal:yyyy-MM-dd} {o}"));
            respuesta.JornadasCreadas.Add(ToResponse(jornada));
        }

        return respuesta;
    }

    /// <summary>
    /// Vuelve a pasar el horario a asignaciones de una jornada (por ejemplo
    /// después de cambiar el horario). Solo agrega lo que falta.
    /// </summary>
    public async Task<ResultadoGeneracion> GenerateAssignmentsAsync(long id)
    {
        var jornada = await _db.Jornadas
            .FirstOrDefaultAsync(j => j.Id == id)
            ?? throw new KeyNotFoundException("Jornada no encontrada.");

        return await _generador.GenerarAsync(jornada);
    }

    public async Task<List<JornadaResponse>> GetAllAsync()
    {
        return await _db.Jornadas
            .AsNoTracking()
            .OrderByDescending(j => j.FechaLocal)
            .Select(j => new JornadaResponse
            {
                Id = j.Id,
                FechaLocal = j.FechaLocal,
                ZonaHoraria = j.ZonaHoraria,
                InicioUtc = j.InicioUtc,
                FinUtc = j.FinUtc,
                LimitePuntualUtc = j.LimitePuntualUtc,
                Estado = j.Estado
            })
            .ToListAsync();
    }

    public async Task<JornadaResponse?> GetByIdAsync(long id)
    {
        var jornada = await _db.Jornadas
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id);

        return jornada is null
            ? null
            : ToResponse(jornada);
    }

    public async Task<JornadaResponse> UpdateAsync(
        long id,
        UpdateJornadaRequest request,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.ZonaHoraria))
            throw new ArgumentException(
                "La zona horaria es obligatoria.");

        if (request.FinUtc <= request.InicioUtc)
            throw new ArgumentException(
                "FinUtc debe ser posterior a InicioUtc.");

        if (request.LimitePuntualUtc < request.InicioUtc ||
            request.LimitePuntualUtc > request.FinUtc)
        {
            throw new ArgumentException(
                "LimitePuntualUtc debe estar dentro del intervalo de la jornada.");
        }

        var jornada = await _db.Jornadas
            .FirstOrDefaultAsync(j => j.Id == id);

        if (jornada is null)
            throw new KeyNotFoundException(
                "Jornada no encontrada.");

        var antes = new
        {
            jornada.Id,
            jornada.FechaLocal,
            jornada.ZonaHoraria,
            jornada.InicioUtc,
            jornada.FinUtc,
            jornada.LimitePuntualUtc,
            jornada.Estado
        };

        jornada.ZonaHoraria = request.ZonaHoraria.Trim();
        jornada.InicioUtc = request.InicioUtc;
        jornada.FinUtc = request.FinUtc;
        jornada.LimitePuntualUtc = request.LimitePuntualUtc;

        _auditService.Registrar(
            user,
            "Jornada",
            jornada.Id.ToString(),
            "ACTUALIZAR",
            "Actualización de jornada.",
            antes: antes,
            despues: new
            {
                jornada.Id,
                jornada.FechaLocal,
                jornada.ZonaHoraria,
                jornada.InicioUtc,
                jornada.FinUtc,
                jornada.LimitePuntualUtc,
                jornada.Estado
            });

        await _db.SaveChangesAsync();

        return ToResponse(jornada);
    }

    public async Task SetStatusAsync(
        long id,
        string estado,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(estado) ||
            !Estados.TryGetValue(estado.Trim(), out var estadoNormalizado))
            throw new ArgumentException(
                "Estado de jornada no válido (PLANIFICADA, ABIERTA, CERRADA o CANCELADA).");

        estado = estadoNormalizado;

        var jornada = await _db.Jornadas
            .FirstOrDefaultAsync(j => j.Id == id);

        if (jornada is null)
            throw new KeyNotFoundException(
                "Jornada no encontrada.");

        var estadoAnterior = jornada.Estado;

        if (estadoAnterior == estado)
            throw new InvalidOperationException(
                "La jornada ya tiene ese estado.");

        jornada.Estado = estado;

        _auditService.Registrar(
            user,
            "Jornada",
            jornada.Id.ToString(),
            "CAMBIAR_ESTADO",
            $"Cambio de estado de jornada de {estadoAnterior} a {estado}.",
            antes: new
            {
                jornada.Id,
                Estado = estadoAnterior
            },
            despues: new
            {
                jornada.Id,
                jornada.Estado
            });

        await _db.SaveChangesAsync();
    }

    private static JornadaResponse ToResponse(Jornada jornada)
    {
        return new JornadaResponse
        {
            Id = jornada.Id,
            FechaLocal = jornada.FechaLocal,
            ZonaHoraria = jornada.ZonaHoraria,
            InicioUtc = jornada.InicioUtc,
            FinUtc = jornada.FinUtc,
            LimitePuntualUtc = jornada.LimitePuntualUtc,
            Estado = jornada.Estado
        };
    }
}