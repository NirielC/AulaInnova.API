using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Attendance;
using AulaInnova.API.DTOs.Estacion;
using AulaInnova.API.Models;
using AulaInnova.API.Services.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AulaInnova.API.Services;

/// <summary>
/// Todo lo que una estación física (NFC o cámara) le pide a la API con su
/// propio secreto (esquema "Estacion"): saber quién pasó la tarjeta,
/// registrar la lectura, conocer su configuración, avisar que sigue viva y,
/// en el caso de la cámara, saber qué clase hay ahora en su aula.
/// </summary>
public class EstacionService
{
    // Dos lecturas de la misma tarjeta de personal en menos de este tiempo
    // son la misma (tarjeta apoyada o pasada dos veces seguidas).
    private static readonly TimeSpan AntirrebotePersonal = TimeSpan.FromMinutes(1);

    // Un docente puede marcar llegada un poco antes de su clase o salida
    // un poco después de que termine.
    private static readonly TimeSpan MargenClase = TimeSpan.FromMinutes(15);

    private readonly AulaInnovaDbContext _db;
    private readonly AttendanceService _attendanceService;
    private readonly AsignacionGeneradorService _generador;

    public EstacionService(
        AulaInnovaDbContext db,
        AttendanceService attendanceService,
        AsignacionGeneradorService generador)
    {
        _db = db;
        _attendanceService = attendanceService;
        _generador = generador;
    }

    // ================================================================
    //  Configuración, latido y credenciales
    // ================================================================

    public async Task<ConfiguracionEstacionResponse> ConfiguracionAsync(
        long estacionId)
    {
        var ahora = DateTime.UtcNow;

        await RegistrarComunicacionAsync(estacionId, null, ahora);

        var codigoLogico = await _db.Estaciones
            .Where(e => e.Id == estacionId)
            .Select(e => e.CodigoLogico)
            .FirstAsync();

        var configuracion = await EstacionReglas.ConfiguracionVigenteAsync(
            _db, estacionId, ahora);

        var aulaCodigo = configuracion.AulaId is null
            ? null
            : await _db.Aulas
                .Where(a => a.Id == configuracion.AulaId)
                .Select(a => a.Codigo)
                .FirstOrDefaultAsync();

        var acciones = await _db.EstacionesOperaciones
            .Where(o => o.EstacionConfiguracionId == configuracion.Id)
            .Select(o => o.Accion)
            .ToListAsync();

        var modos = new List<string>();

        if (acciones.Contains("ASISTENCIA"))
            modos.Add("ASISTENCIA");

        if (acciones.Contains("SALIDA_AULA") ||
            acciones.Contains("REGRESO_AULA"))
            modos.Add("MOVIMIENTOS");

        var motivos = await _db.MotivosSalida
            .AsNoTracking()
            .Where(m => m.Habilitado)
            .OrderBy(m => m.Codigo)
            .ToListAsync();

        return new ConfiguracionEstacionResponse
        {
            EstacionId = estacionId,
            CodigoLogico = codigoLogico,
            TipoPunto = configuracion.TipoPunto,
            AulaId = configuracion.AulaId,
            AulaCodigo = aulaCodigo,
            EtiquetaPunto = configuracion.EtiquetaPunto,
            ExpiracionSelectorS = configuracion.ExpiracionSelectorS,
            Modos = modos,
            Motivos = motivos
                .Select(m => new MotivoEstacion
                {
                    Codigo = m.Codigo,
                    Nombre = EstacionReglas.SoloAscii(m.Nombre)
                })
                .ToList(),
            ServidorUtc = ahora
        };
    }

    public async Task<HeartbeatResponse> HeartbeatAsync(
        long estacionId,
        HeartbeatRequest request)
    {
        var ahora = DateTime.UtcNow;

        var modo = string.IsNullOrWhiteSpace(request.Modo)
            ? null
            : NormalizarModo(request.Modo);

        await RegistrarComunicacionAsync(estacionId, modo, ahora);

        return new HeartbeatResponse { ServidorUtc = ahora };
    }

    public async Task<CredencialEstacionResponse> CredencialAsync(
        long estacionId,
        string referenciaNfc)
    {
        var ahora = DateTime.UtcNow;

        await RegistrarComunicacionAsync(estacionId, null, ahora);

        var credencial = await EstacionReglas.CredencialAsync(
            _db, referenciaNfc, ahora);

        var tipo = await EstacionReglas.TipoPersonaAsync(
            _db, credencial.Persona);

        return new CredencialEstacionResponse
        {
            TipoPersona = tipo,
            NombreCorto = EstacionReglas.NombreCorto(credencial.Persona),
            EsAdministrador = tipo == "ADMINISTRADOR"
        };
    }

    // ================================================================
    //  Lectura de tarjeta
    // ================================================================

    public async Task<LecturaEstacionResponse> LecturaAsync(
        long estacionId,
        LecturaEstacionRequest request)
    {
        var modo = NormalizarModo(request.Modo);
        var utc = EstacionReglas.HoraDeLectura(request.OcurridoUtc);

        // Una lectura en vivo también cuenta como latido.
        await RegistrarComunicacionAsync(
            estacionId,
            request.OcurridoUtc.HasValue ? null : modo,
            DateTime.UtcNow);

        var credencial = await EstacionReglas.CredencialAsync(
            _db, request.ReferenciaNfc, utc);

        var tipo = await EstacionReglas.TipoPersonaAsync(
            _db, credencial.Persona);

        var nombre = EstacionReglas.NombreCorto(credencial.Persona);

        if (tipo != "ESTUDIANTE")
        {
            return await PresenciaPersonalAsync(
                estacionId, request, utc, credencial, tipo, nombre);
        }

        if (modo == "ASISTENCIA")
        {
            var asistencia = await _attendanceService.RegisterAsync(
                new AttendanceRequest
                {
                    ReferenciaNfc = credencial.Referencia,
                    EstacionId = estacionId,
                    OcurridoUtc = request.OcurridoUtc
                });

            return new LecturaEstacionResponse
            {
                Resultado = asistencia.YaRegistrada
                    ? "ASISTENCIA_YA_REGISTRADA"
                    : "ASISTENCIA_REGISTRADA",
                TipoPersona = tipo,
                NombreCorto = nombre,
                Puntualidad = asistencia.Puntualidad
            };
        }

        // MOVIMIENTOS: todavía no se registra nada. La estación pregunta
        // primero si el estudiante está dentro (elige motivo de salida) o
        // fuera (confirma su regreso). La cola offline manda directamente
        // la salida o el regreso a /api/Movement, nunca esta lectura.
        if (request.OcurridoUtc.HasValue)
            throw new RechazoEstacionException(
                "LECTURA_SIN_ACCION",
                "Sin red, en modo MOVIMIENTOS se guarda la salida o el regreso, no la lectura.");

        var configuracion = await EstacionReglas.ConfiguracionVigenteAsync(
            _db, estacionId, utc);

        await EstacionReglas.ExigirOperacionAsync(
            _db, configuracion.Id, "SALIDA_AULA");

        var jornada = await EstacionReglas.JornadaObligatoriaAsync(_db, utc);

        await EstacionReglas.MatriculaAsync(
            _db, credencial.PersonaId, jornada);

        var salida = await _db.SalidasTemporales
            .AsNoTracking()
            .Where(s =>
                s.EstudianteId == credencial.PersonaId &&
                s.JornadaId == jornada.Id &&
                s.Estado == "ABIERTA")
            .OrderByDescending(s => s.SalidaUtc)
            .FirstOrDefaultAsync();

        return new LecturaEstacionResponse
        {
            Resultado = salida is null ? "ELEGIR_MOTIVO" : "CONFIRMAR_REGRESO",
            TipoPersona = tipo,
            NombreCorto = nombre,
            MotivoSalidaAbierta = salida?.MotivoCodigo,
            SalidaAbiertaUtc = salida?.SalidaUtc
        };
    }

    /// <summary>
    /// Profesor, auxiliar o administrativo que pasa su tarjeta en un aula:
    /// la primera lectura es LLEGADA_PERSONAL y la siguiente SALIDA_PERSONAL,
    /// ligadas a su clase (asignación) de esa hora en esa aula. Es lo que
    /// lee ai.vw_cobertura_aula para saber si el aula tiene adulto.
    /// Sin clase suya en el aula se guarda igual, pero como POR_REVISAR.
    /// </summary>
    private async Task<LecturaEstacionResponse> PresenciaPersonalAsync(
        long estacionId,
        LecturaEstacionRequest request,
        DateTime utc,
        CredencialNfc credencial,
        string tipo,
        string nombre)
    {
        var (seleccionId, seleccionEdadMs) = EstacionReglas.Seleccion(
            request.SeleccionId, request.SeleccionEdadMs);

        var repetido = await EstacionReglas.EventoPorSeleccionAsync(
            _db, estacionId, seleccionId);

        if (repetido is not null)
            return await RespuestaPersonalAsync(repetido, tipo, nombre, yaRegistrado: true);

        var configuracion = await EstacionReglas.ConfiguracionVigenteAsync(
            _db, estacionId, utc);

        // LLEGADA y SALIDA_PERSONAL se habilitan juntas (AdminEstacionService):
        // una estación sin ellas (p. ej. la cámara) no registra personal.
        await EstacionReglas.ExigirOperacionAsync(
            _db, configuracion.Id, "LLEGADA_PERSONAL");

        var jornada = await EstacionReglas.JornadaDeAsync(_db, utc);

        var asignacion = await AsignacionDelPersonalAsync(
            credencial.PersonaId, configuracion.AulaId, jornada, utc);

        var anterior = await UltimoEventoPersonalAsync(
            credencial.PersonaId, asignacion?.Id, configuracion.AulaId, jornada?.Id, utc);

        if (anterior?.OcurridoUtc is DateTime horaAnterior &&
            (utc - horaAnterior).Duration() < AntirrebotePersonal)
        {
            return await RespuestaPersonalAsync(anterior, tipo, nombre, yaRegistrado: true);
        }

        var accion = anterior?.Accion == "LLEGADA_PERSONAL"
            ? "SALIDA_PERSONAL"
            : "LLEGADA_PERSONAL";

        await EstacionReglas.ExigirOperacionAsync(_db, configuracion.Id, accion);

        var evento = new Evento
        {
            Id = Guid.NewGuid(),
            HashSolicitud = EstacionReglas.Hash(
                $"{credencial.Referencia}|{estacionId}|{accion}|{utc:O}"),
            Origen = "ESTACION",
            Accion = accion,
            PersonaId = credencial.PersonaId,
            CredencialId = credencial.Id,
            EstacionId = estacionId,
            EstacionConfiguracionId = configuracion.Id,
            AulaId = configuracion.AulaId,
            JornadaId = jornada?.Id,
            AsignacionId = asignacion?.Id,
            OcurridoUtc = utc,
            RecibidoUtc = DateTime.UtcNow,
            CalidadHora = EstacionReglas.CalidadHora(request.OcurridoUtc),
            Estado = asignacion is null ? "POR_REVISAR" : "ACEPTADO",
            CodigoResultado = asignacion is null
                ? "SIN_ASIGNACION_EN_AULA"
                : accion == "LLEGADA_PERSONAL"
                    ? "LLEGADA_REGISTRADA"
                    : "SALIDA_REGISTRADA",
            SeleccionId = seleccionId,
            SeleccionEdadMs = seleccionEdadMs
        };

        _db.Eventos.Add(evento);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // La misma lectura llegó dos veces a la vez.
            _db.ChangeTracker.Clear();

            var guardado = await EstacionReglas.EventoPorSeleccionAsync(
                _db, estacionId, seleccionId);

            if (guardado is null)
                throw;

            return await RespuestaPersonalAsync(guardado, tipo, nombre, yaRegistrado: true);
        }

        return await RespuestaPersonalAsync(evento, tipo, nombre, yaRegistrado: false);
    }

    private async Task<Asignacion?> AsignacionDelPersonalAsync(
        long personaId,
        long? aulaId,
        Jornada? jornada,
        DateTime utc)
    {
        if (aulaId is null || jornada is null)
            return null;

        // Por si el horario cambió después de crear la jornada.
        await _generador.GenerarAsync(jornada);

        var desde = utc - MargenClase;
        var hasta = utc + MargenClase;

        var candidatas = await _db.Asignaciones
            .AsNoTracking()
            .Where(a =>
                a.PersonalId == personaId &&
                a.AulaId == aulaId &&
                a.Estado == "ACTIVA" &&
                a.InicioUtc <= hasta &&
                a.FinUtc >= desde)
            .ToListAsync();

        // Primero la clase que está en curso; si no, la más cercana.
        return candidatas
            .OrderByDescending(a => a.InicioUtc <= utc && a.FinUtc >= utc)
            .ThenBy(a => (a.InicioUtc - utc).Duration())
            .FirstOrDefault();
    }

    private Task<Evento?> UltimoEventoPersonalAsync(
        long personaId,
        long? asignacionId,
        long? aulaId,
        long? jornadaId,
        DateTime utc)
    {
        var limite = utc + AntirrebotePersonal;

        var eventos = _db.Eventos
            .AsNoTracking()
            .Where(e =>
                e.PersonaId == personaId &&
                (e.Accion == "LLEGADA_PERSONAL" || e.Accion == "SALIDA_PERSONAL") &&
                e.OcurridoUtc <= limite);

        eventos = asignacionId is not null
            ? eventos.Where(e => e.AsignacionId == asignacionId)
            : eventos.Where(e =>
                e.AsignacionId == null &&
                e.JornadaId == jornadaId &&
                e.AulaId == aulaId);

        return eventos
            .OrderByDescending(e => e.OcurridoUtc)
            .FirstOrDefaultAsync();
    }

    private async Task<LecturaEstacionResponse> RespuestaPersonalAsync(
        Evento evento,
        string tipo,
        string nombre,
        bool yaRegistrado)
    {
        string? clase = null;

        if (evento.AsignacionId is long asignacionId)
        {
            clase = await DescribirAsignacionAsync(asignacionId);
        }

        return new LecturaEstacionResponse
        {
            Resultado = yaRegistrado ? "PERSONAL_YA_REGISTRADO" : evento.Accion,
            Accion = evento.Accion,
            TipoPersona = tipo,
            NombreCorto = nombre,
            AsignacionId = evento.AsignacionId,
            Clase = clase,
            Aceptado = evento.Estado == "ACEPTADO"
        };
    }

    // ================================================================
    //  Cámara: sesión visual de la clase actual
    // ================================================================

    /// <summary>
    /// La cámara lo llama cada minuto. La API mira el horario (asignaciones
    /// del día) del aula de la cámara y:
    ///   - cierra las sesiones de clases que ya terminaron;
    ///   - abre la sesión de la clase que está en curso, si no existe;
    ///   - devuelve su id y los ids de zonas e indicadores.
    /// Así nadie abre ni cierra sesiones a mano: el cambio de clase es solo.
    ///
    /// Autorización: la sesión queda autorizada por el administrador que
    /// configuró esta estación (cámara) en el aula. Se abre para clases
    /// (CLASE) y sustituciones (SUSTITUCION), no para cuidos.
    /// </summary>
    public async Task<SesionVisionResponse> SesionVisionAsync(
        long estacionId,
        SesionVisionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.VersionProceso) ||
            request.VersionProceso.Trim().Length > 50)
        {
            throw new RechazoEstacionException(
                "VERSION_INVALIDA",
                "VersionProceso es obligatoria (máximo 50 caracteres).");
        }

        var versionProceso = request.VersionProceso.Trim();
        var utc = DateTime.UtcNow;

        await RegistrarComunicacionAsync(estacionId, null, utc);

        var configuracion = await EstacionReglas.ConfiguracionVigenteAsync(
            _db, estacionId, utc);

        if (configuracion.AulaId is not long aulaId)
            throw new RechazoEstacionException(
                "ESTACION_SIN_AULA",
                "La cámara debe estar configurada como estación de AULA.");

        var jornada = await EstacionReglas.JornadaDeAsync(_db, utc);

        Asignacion? actual = null;

        if (jornada is not null)
        {
            await _generador.GenerarAsync(jornada);

            actual = await _db.Asignaciones
                .AsNoTracking()
                .Where(a =>
                    a.AulaId == aulaId &&
                    a.Estado == "ACTIVA" &&
                    (a.Tipo == "CLASE" || a.Tipo == "SUSTITUCION") &&
                    a.InicioUtc <= utc &&
                    a.FinUtc > utc)
                .OrderBy(a => a.InicioUtc)
                .FirstOrDefaultAsync();
        }

        // 1. Cerrar lo que ya no corresponde (clase terminada o cambiada).
        var activas = await _db.SesionesVisuales
            .Include(s => s.Asignacion)
            .Where(s => s.AulaId == aulaId && s.Estado == "ACTIVA")
            .ToListAsync();

        SesionVisual? sesion = null;

        foreach (var activa in activas)
        {
            if (actual is not null && activa.AsignacionId == actual.Id)
            {
                sesion = activa;
                continue;
            }

            var fin = activa.Asignacion.FinUtc < utc
                ? activa.Asignacion.FinUtc
                : utc;

            activa.FinUtc = fin < activa.InicioUtc ? activa.InicioUtc : fin;
            activa.Estado = "CERRADA";
        }

        await _db.SaveChangesAsync();

        // 2. Abrir la de la clase en curso (UX_sesion_visual_aula_activa:
        //    una sola activa por aula; por eso se guarda después de cerrar).
        if (actual is not null && sesion is null)
        {
            var codigoEstacion = await _db.Estaciones
                .Where(e => e.Id == estacionId)
                .Select(e => e.CodigoLogico)
                .FirstAsync();

            var referencia =
                $"Apertura automática por horario desde la estación {codigoEstacion}. " +
                $"Autorizada al configurar la estación (configuración {configuracion.Id}): " +
                configuracion.MotivoCambio;

            sesion = new SesionVisual
            {
                AsignacionId = actual.Id,
                AulaId = aulaId,
                AutorizadaPor = configuracion.ConfiguradaPor,
                ReferenciaAutorizacion = referencia.Length > 200
                    ? referencia[..200]
                    : referencia,
                InicioUtc = utc,
                FinUtc = null,
                VersionProceso = versionProceso,
                Estado = "ACTIVA"
            };

            _db.SesionesVisuales.Add(sesion);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Otra llamada abrió la sesión al mismo tiempo: la cámara
                // vuelve a preguntar en el siguiente minuto y la recibe.
                throw new ConcurrentConflictException(
                    "Otra solicitud abrió la sesión visual de esta aula al mismo tiempo.");
            }
        }

        var respuesta = new SesionVisionResponse
        {
            AulaId = aulaId,
            ServidorUtc = utc,
            Zonas = await ZonasDelAulaAsync(aulaId),
            Indicadores = await IndicadoresHabilitadosAsync()
        };

        if (actual is null || sesion is null)
        {
            respuesta.Mensaje = jornada is null
                ? "Sin jornada escolar a esta hora."
                : "No hay clase en el aula a esta hora.";

            return respuesta;
        }

        respuesta.SesionId = sesion.Id;
        respuesta.SesionInicioUtc = DateTime.SpecifyKind(sesion.InicioUtc, DateTimeKind.Utc);
        respuesta.AsignacionId = actual.Id;
        respuesta.Clase = await DescribirAsignacionAsync(actual.Id);
        respuesta.ClaseInicioUtc = DateTime.SpecifyKind(actual.InicioUtc, DateTimeKind.Utc);
        respuesta.ClaseFinUtc = DateTime.SpecifyKind(actual.FinUtc, DateTimeKind.Utc);
        respuesta.Mensaje = "Clase en curso.";

        return respuesta;
    }

    private async Task<Dictionary<string, long>> ZonasDelAulaAsync(long aulaId)
    {
        var zonas = await _db.ZonasVisuales
            .AsNoTracking()
            .Where(z => z.AulaId == aulaId && z.Habilitada)
            .Select(z => new { z.Codigo, z.Id, z.Revision })
            .ToListAsync();

        return zonas
            .GroupBy(z => z.Codigo)
            .ToDictionary(g => g.Key, g => g.MaxBy(z => z.Revision)!.Id);
    }

    private async Task<Dictionary<string, long>> IndicadoresHabilitadosAsync()
    {
        var indicadores = await _db.IndicadoresVisuales
            .AsNoTracking()
            .Where(i => i.Habilitado)
            .Select(i => new { i.Codigo, i.Id, i.Revision })
            .ToListAsync();

        return indicadores
            .GroupBy(i => i.Codigo)
            .ToDictionary(g => g.Key, g => g.MaxBy(i => i.Revision)!.Id);
    }

    // ================================================================
    //  Utilidades
    // ================================================================

    /// <summary>"Matematica 1 A" (sin tildes, para la LCD y la consola).</summary>
    private async Task<string?> DescribirAsignacionAsync(long asignacionId)
    {
        var datos = await _db.Asignaciones
            .AsNoTracking()
            .Where(a => a.Id == asignacionId)
            .Select(a => new
            {
                Asignatura = a.HorarioClase != null
                    ? a.HorarioClase.Asignatura.Nombre
                    : a.Actividad,
                a.Tipo,
                a.Grupo.Grado,
                a.Grupo.Seccion
            })
            .FirstOrDefaultAsync();

        if (datos is null)
            return null;

        var que = string.IsNullOrWhiteSpace(datos.Asignatura)
            ? datos.Tipo
            : datos.Asignatura;

        return EstacionReglas.SoloAscii($"{que} {datos.Grado} {datos.Seccion}").Trim();
    }

    /// <summary>
    /// Actualiza estacion.ultima_comunicacion_utc (y el modo reportado)
    /// sin cargar la fila: así no compite con otras solicitudes por la
    /// columna rowversion.
    /// </summary>
    private Task RegistrarComunicacionAsync(
        long estacionId,
        string? modo,
        DateTime ahora)
    {
        var estacion = _db.Estaciones.Where(e => e.Id == estacionId);

        if (modo is null)
        {
            return estacion.ExecuteUpdateAsync(s => s
                .SetProperty(e => e.UltimaComunicacionUtc, ahora));
        }

        // CK_estacion_modo_hora: modo_reportado y modo_reportado_utc van juntos.
        return estacion.ExecuteUpdateAsync(s => s
            .SetProperty(e => e.UltimaComunicacionUtc, ahora)
            .SetProperty(e => e.ModoReportado, modo)
            .SetProperty(e => e.ModoReportadoUtc, ahora));
    }

    private static string NormalizarModo(string? modo)
    {
        var valor = modo?.Trim().ToUpperInvariant();

        if (valor is "ASISTENCIA" or "MOVIMIENTOS")
            return valor;

        throw new RechazoEstacionException(
            "MODO_INVALIDO",
            "El modo debe ser ASISTENCIA o MOVIMIENTOS.");
    }
}
