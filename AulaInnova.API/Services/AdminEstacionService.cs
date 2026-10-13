using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Admin.Estacion;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Services;

public class AdminEstacionService
{
    private readonly AulaInnovaDbContext _db;
    private readonly AuditService _auditService;

    // CK_estacion_configuracion_tipo_punto: AULA o ACCESO (entrada del
    // colegio). "ENTRADA" se acepta como sinónimo de ACCESO.
    private static readonly string[] TiposPuntoPermitidos =
    {
        "ACCESO",
        "AULA"
    };

    // CK_estacion_configuracion_expiracion: entre 3 y 60 segundos.
    private const short ExpiracionSelectorPorDefecto = 15;

    private static readonly string[] ModosPermitidos =
    {
        "ASISTENCIA",
        "MOVIMIENTOS"
    };

    public AdminEstacionService(
        AulaInnovaDbContext db,
        AuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    public async Task<EstacionResponse> CreateAsync(
        CreateEstacionRequest request,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.CodigoLogico))
            throw new ArgumentException(
                "El código lógico es obligatorio.");

        var codigo = request.CodigoLogico.Trim();

        var exists = await _db.Estaciones
            .AnyAsync(e => e.CodigoLogico == codigo);

        if (exists)
            throw new InvalidOperationException(
                "Ya existe una estación con ese código lógico.");

        var estacion = new Estacion
        {
            CodigoLogico = codigo,
            MacInventario = string.IsNullOrWhiteSpace(request.MacInventario)
                ? null
                : request.MacInventario.Trim(),
            Habilitada = true,
            CreadoUtc = DateTime.UtcNow
        };

        _db.Estaciones.Add(estacion);

        _auditService.Registrar(
            user,
            "Estacion",
            "NUEVA",
            "CREAR",
            "Alta de estación.",
            antes: null,
            despues: new
            {
                estacion.CodigoLogico,
                estacion.MacInventario,
                estacion.Habilitada
            });

        await _db.SaveChangesAsync();

        return ToResponse(estacion);
    }

    public async Task<List<EstacionResponse>> GetAllAsync()
    {
        return await _db.Estaciones
            .AsNoTracking()
            .OrderBy(e => e.CodigoLogico)
            .Select(e => new EstacionResponse
            {
                Id = e.Id,
                CodigoLogico = e.CodigoLogico,
                Habilitada = e.Habilitada,
                MacInventario = e.MacInventario,
                UltimaComunicacionUtc = e.UltimaComunicacionUtc,
                ModoReportado = e.ModoReportado
            })
            .ToListAsync();
    }

    public async Task<EstacionResponse?> GetByIdAsync(long id)
    {
        var estacion = await _db.Estaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        return estacion is null
            ? null
            : ToResponse(estacion);
    }

    public async Task SetStatusAsync(
        long id,
        bool habilitada,
        ClaimsPrincipal user)
    {
        var estacion = await _db.Estaciones
            .FirstOrDefaultAsync(e => e.Id == id);

        if (estacion is null)
            throw new KeyNotFoundException(
                "Estación no encontrada.");

        var antes = new
        {
            estacion.Id,
            estacion.CodigoLogico,
            estacion.Habilitada
        };

        estacion.Habilitada = habilitada;

        _auditService.Registrar(
            user,
            "Estacion",
            estacion.Id.ToString(),
            habilitada ? "HABILITAR" : "DESHABILITAR",
            habilitada
                ? "Habilitación de estación."
                : "Deshabilitación de estación.",
            antes: antes,
            despues: new
            {
                estacion.Id,
                estacion.CodigoLogico,
                estacion.Habilitada
            });

        await _db.SaveChangesAsync();
    }

    public async Task<EstacionConfigurationResponse> ConfigureAsync(
        long estacionId,
        ConfigureEstacionRequest request,
        Guid usuarioId,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.TipoPunto))
            throw new ArgumentException(
                "El tipo de punto es obligatorio.");

        if (string.IsNullOrWhiteSpace(request.EtiquetaPunto))
            throw new ArgumentException(
                "La etiqueta del punto es obligatoria.");

        if (string.IsNullOrWhiteSpace(request.MotivoCambio))
            throw new ArgumentException(
                "El motivo del cambio es obligatorio.");

        var tipoPunto = request.TipoPunto
            .Trim()
            .ToUpperInvariant();

        if (tipoPunto == "ENTRADA")
            tipoPunto = "ACCESO";

        if (!TiposPuntoPermitidos.Contains(tipoPunto))
            throw new ArgumentException(
                "TipoPunto no válido (AULA o ACCESO).");

        var expiracionSelectorS = request.ExpiracionSelectorS == 0
            ? ExpiracionSelectorPorDefecto
            : request.ExpiracionSelectorS;

        if (expiracionSelectorS < 3 || expiracionSelectorS > 60)
            throw new ArgumentException(
                "ExpiracionSelectorS debe estar entre 3 y 60 segundos.");

        var estacion = await _db.Estaciones
            .FirstOrDefaultAsync(e => e.Id == estacionId);

        if (estacion is null)
            throw new KeyNotFoundException(
                "Estación no encontrada.");

        if (!estacion.Habilitada)
            throw new InvalidOperationException(
                "No se puede configurar una estación deshabilitada.");

        if (tipoPunto == "AULA")
        {
            if (!request.AulaId.HasValue)
                throw new ArgumentException(
                    "Una estación de aula debe tener un aula asignada.");

            var aula = await _db.Aulas
                .FirstOrDefaultAsync(a =>
                    a.Id == request.AulaId.Value);

            if (aula is null || !aula.Habilitada)
                throw new ArgumentException(
                    "El aula no existe o está deshabilitada.");
        }

        if (tipoPunto == "ACCESO" &&
            request.AulaId.HasValue)
        {
            throw new ArgumentException(
                "Una estación de acceso no debe tener aula asignada.");
        }

        var now = DateTime.UtcNow;

        var current = await _db.EstacionesConfiguracion
            .Where(c =>
                c.EstacionId == estacionId &&
                c.VigenteDesdeUtc <= now &&
                (c.VigenteHastaUtc == null ||
                 c.VigenteHastaUtc > now))
            .FirstOrDefaultAsync();

        object? antes = null;

        // Si al reconfigurar no se indica ningún modo, se conservan las
        // operaciones de la configuración anterior: si no, la estación
        // quedaría sin poder registrar nada hasta llamar a /mode.
        var accionesAnteriores = current is null
            ? new List<string>()
            : await _db.EstacionesOperaciones
                .Where(o => o.EstacionConfiguracionId == current.Id)
                .Select(o => o.Accion)
                .ToListAsync();

        if (current is not null)
        {
            antes = new
            {
                current.Id,
                current.EstacionId,
                current.TipoPunto,
                current.AulaId,
                current.EtiquetaPunto,
                current.ExpiracionSelectorS,
                current.VigenteDesdeUtc,
                current.VigenteHastaUtc,
                current.ConfiguradaPor,
                current.MotivoCambio
            };

            current.VigenteHastaUtc = now;
        }

        var configuration = new EstacionConfiguracion
        {
            EstacionId = estacionId,
            TipoPunto = tipoPunto,
            AulaId = request.AulaId,
            EtiquetaPunto = request.EtiquetaPunto.Trim(),
            ExpiracionSelectorS = expiracionSelectorS,
            VigenteDesdeUtc = now,
            VigenteHastaUtc = null,
            ConfiguradaPor = usuarioId,
            MotivoCambio = request.MotivoCambio.Trim(),
            CreadoUtc = now
        };

        var acciones = new List<string>();

        if (request.PermiteAsistencia)
            acciones.AddRange(AccionesDelModo("ASISTENCIA"));

        if (request.PermiteMovimientos)
            acciones.AddRange(AccionesDelModo("MOVIMIENTOS"));

        if (acciones.Count == 0)
            acciones.AddRange(accionesAnteriores);

        foreach (var accion in acciones.Distinct())
        {
            configuration.Operaciones.Add(new EstacionOperacion
            {
                Accion = accion,
                ModoInterfaz = ModoInterfazDe(accion)
            });
        }

        _db.EstacionesConfiguracion.Add(configuration);

        _auditService.Registrar(
            user,
            "EstacionConfiguracion",
            "NUEVA",
            "CONFIGURAR",
            request.MotivoCambio.Trim(),
            antes: antes,
            despues: new
            {
                configuration.EstacionId,
                configuration.TipoPunto,
                configuration.AulaId,
                configuration.EtiquetaPunto,
                configuration.ExpiracionSelectorS,
                configuration.VigenteDesdeUtc,
                configuration.VigenteHastaUtc,
                configuration.ConfiguradaPor,
                configuration.MotivoCambio
            });

        await _db.SaveChangesAsync();

        return ToConfigurationResponse(configuration);
    }

    public async Task<EstacionConfigurationResponse?>
        GetCurrentConfigurationAsync(long estacionId)
    {
        var now = DateTime.UtcNow;

        var configuration = await _db.EstacionesConfiguracion
            .AsNoTracking()
            .Where(c =>
                c.EstacionId == estacionId &&
                c.VigenteDesdeUtc <= now &&
                (c.VigenteHastaUtc == null ||
                 c.VigenteHastaUtc > now))
            .FirstOrDefaultAsync();

        return configuration is null
            ? null
            : ToConfigurationResponse(configuration);
    }

    public async Task ConfigureModeAsync(
        long estacionId,
        string modo,
        Guid usuarioId,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(modo))
            throw new ArgumentException(
                "El modo es obligatorio.");

        modo = modo.Trim().ToUpperInvariant();

        if (!ModosPermitidos.Contains(modo))
            throw new ArgumentException(
                "El modo de estación no es válido.");

        var estacion = await _db.Estaciones
            .FirstOrDefaultAsync(e => e.Id == estacionId);

        if (estacion is null)
            throw new KeyNotFoundException(
                "Estación no encontrada.");

        if (!estacion.Habilitada)
            throw new InvalidOperationException(
                "La estación está deshabilitada.");

        var now = DateTime.UtcNow;

        var configuration = await _db.EstacionesConfiguracion
            .FirstOrDefaultAsync(c =>
                c.EstacionId == estacionId &&
                c.VigenteDesdeUtc <= now &&
                (c.VigenteHastaUtc == null ||
                 c.VigenteHastaUtc > now));

        if (configuration is null)
            throw new InvalidOperationException(
                "La estación no tiene una configuración vigente.");

        var existentes = await _db.EstacionesOperaciones
            .Where(o => o.EstacionConfiguracionId == configuration.Id)
            .Select(o => o.Accion)
            .ToListAsync();

        foreach (var accion in AccionesDelModo(modo).Except(existentes))
        {
            _db.EstacionesOperaciones.Add(new EstacionOperacion
            {
                EstacionConfiguracionId = configuration.Id,
                Accion = accion,
                ModoInterfaz = ModoInterfazDe(accion)
            });
        }

        _auditService.Registrar(
            user,
            "EstacionConfiguracion",
            configuration.Id.ToString(),
            "CONFIGURAR_MODO",
            $"Configuración del modo {modo}.",
            antes: null,
            despues: new
            {
                configuration.Id,
                configuration.EstacionId,
                Modo = modo,
                UsuarioId = usuarioId
            });

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Emite un secreto nuevo para la estación y revoca el anterior que
    /// estuviera vigente (rotación). El valor en claro solo se devuelve
    /// una vez en la respuesta; en ai.estacion_secreto solo queda su
    /// hash SHA-256.
    /// </summary>
    public async Task<EstacionSecretResponse>
        IssueSecretAsync(
            long estacionId,
            ClaimsPrincipal user)
    {
        var estacion = await _db.Estaciones
            .FirstOrDefaultAsync(e => e.Id == estacionId);

        if (estacion is null)
            throw new KeyNotFoundException(
                "Estación no encontrada.");

        var utcNow = DateTime.UtcNow;

        var vigente = await _db.EstacionesSecretos
            .Where(s =>
                s.EstacionId == estacionId &&
                s.RevocadaUtc == null)
            .ToListAsync();

        foreach (var secretoVigente in vigente)
            secretoVigente.RevocadaUtc = utcNow;

        var secretoPlano = GenerarSecretoAleatorio();
        var identificadorClave = Guid.NewGuid().ToString("N");

        var nuevo = new EstacionSecreto
        {
            EstacionId = estacionId,
            IdentificadorClave = identificadorClave,
            HashSecreto = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(secretoPlano)),
            DesdeUtc = utcNow,
            RevocadaUtc = null
        };

        _db.EstacionesSecretos.Add(nuevo);

        _auditService.Registrar(
            user,
            "EstacionSecreto",
            "NUEVA",
            "ACTIVAR",
            "Emisión de secreto de estación.",
            antes: null,
            despues: new
            {
                nuevo.EstacionId,
                nuevo.IdentificadorClave,
                nuevo.DesdeUtc
            });

        await _db.SaveChangesAsync();

        return new EstacionSecretResponse
        {
            EstacionId = estacionId,
            IdentificadorClave = identificadorClave,
            Secreto = $"{identificadorClave}.{secretoPlano}",
            DesdeUtc = utcNow
        };
    }

    public async Task RevokeSecretAsync(
        long estacionId,
        string identificadorClave,
        ClaimsPrincipal user)
    {
        var secreto = await _db.EstacionesSecretos
            .FirstOrDefaultAsync(s =>
                s.EstacionId == estacionId &&
                s.IdentificadorClave == identificadorClave);

        if (secreto is null)
            throw new KeyNotFoundException(
                "Secreto de estación no encontrado.");

        if (secreto.RevocadaUtc is not null)
            return;

        var antes = new { RevocadaUtc = (DateTime?)null };

        secreto.RevocadaUtc = DateTime.UtcNow;

        _auditService.Registrar(
            user,
            "EstacionSecreto",
            secreto.Id.ToString(),
            "REVOCAR",
            "Revocación manual de secreto de estación.",
            antes: antes,
            despues: new { secreto.RevocadaUtc });

        await _db.SaveChangesAsync();
    }

    private static string GenerarSecretoAleatorio()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator
            .GetBytes(32);

        return Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");
    }

    private static EstacionResponse ToResponse(
        Estacion estacion)
    {
        return new EstacionResponse
        {
            Id = estacion.Id,
            CodigoLogico = estacion.CodigoLogico,
            Habilitada = estacion.Habilitada,
            MacInventario = estacion.MacInventario,
            UltimaComunicacionUtc =
                estacion.UltimaComunicacionUtc,
            ModoReportado = estacion.ModoReportado
        };
    }

    private static EstacionConfigurationResponse
        ToConfigurationResponse(
            EstacionConfiguracion configuration)
    {
        return new EstacionConfigurationResponse
        {
            Id = configuration.Id,
            EstacionId = configuration.EstacionId,
            TipoPunto = configuration.TipoPunto,
            AulaId = configuration.AulaId,
            EtiquetaPunto = configuration.EtiquetaPunto,
            ExpiracionSelectorS =
                configuration.ExpiracionSelectorS,
            VigenteDesdeUtc =
                configuration.VigenteDesdeUtc,
            VigenteHastaUtc =
                configuration.VigenteHastaUtc,
            ConfiguradaPor =
                configuration.ConfiguradaPor,
            MotivoCambio =
                configuration.MotivoCambio
        };
    }

    /// <summary>
    /// Acciones (ai.estacion_operacion) que habilita cada modo. La
    /// presencia del personal se habilita en los dos modos: un docente
    /// marca su llegada aunque la estación esté en ASISTENCIA.
    /// </summary>
    private static string[] AccionesDelModo(string modo) =>
        modo == "ASISTENCIA"
            ? new[] { "ASISTENCIA", "LLEGADA_PERSONAL", "SALIDA_PERSONAL" }
            : new[] { "SALIDA_AULA", "REGRESO_AULA", "LLEGADA_PERSONAL", "SALIDA_PERSONAL" };

    // CK_estacion_operacion_coherencia: solo ASISTENCIA usa el modo de
    // interfaz ASISTENCIA; todo lo demás se guarda como MOVIMIENTOS.
    private static string ModoInterfazDe(string accion) =>
        accion == "ASISTENCIA" ? "ASISTENCIA" : "MOVIMIENTOS";
}
