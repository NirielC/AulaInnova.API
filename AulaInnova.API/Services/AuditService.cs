using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AulaInnova.API.Data;
using AulaInnova.API.Models;

namespace AulaInnova.API.Services;

public class AuditService
{
    private readonly AulaInnovaDbContext _db;
    private readonly RequestContext _requestContext;

    public AuditService(
        AulaInnovaDbContext db,
        RequestContext requestContext)
    {
        _db = db;
        _requestContext = requestContext;
    }

    // Valores exactos aceptados por CK_auditoria_entidad (Bloque A) más
    // 'aspnet_user', agregado por 010_ajuste_auditoria_entidad_usuario.sql
    // para poder auditar altas/cambios de dbo.AspNetUsers.
    private static readonly HashSet<string> EntidadesPermitidas = new(StringComparer.Ordinal)
    {
        "persona", "estudiante", "personal", "permiso", "concesion",
        "aula", "grupo", "matricula", "asignatura", "horario_clase",
        "jornada", "credencial_nfc", "estacion",
        "estacion_configuracion", "estacion_secreto",
        "estacion_operacion", "motivo_salida", "evento", "asistencia",
        "salida_temporal", "categoria_incidencia", "tipo_incidencia",
        "incidencia", "observacion_incidencia", "asignacion",
        "necesidad_cobertura", "verificacion_cobertura",
        "falla_estacion", "contexto_clase", "zona_visual",
        "indicador_visual", "sesion_visual", "medicion_visual",
        "aspnet_user"
    };

    // Excepciones puntuales donde la conversión automática PascalCase ->
    // snake_case no da el nombre de tabla real.
    private static readonly Dictionary<string, string> AliasEntidad =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["AspNetUser"] = "aspnet_user"
        };

    // Valores exactos aceptados por CK_auditoria_operacion (Bloque A).
    private static readonly HashSet<string> OperacionesPermitidas = new(StringComparer.Ordinal)
    {
        "CREAR", "CORREGIR", "ANULAR", "JUSTIFICAR", "PUBLICAR",
        "ACTIVAR", "REVOCAR", "CERRAR", "REABRIR"
    };

    // Vocabulario que usan los servicios pero que ai.auditoria no admite
    // literalmente. Se traduce a la operación aprobada más cercana en vez
    // de expandir el CHECK, para no aflojar el catálogo controlado.
    private static readonly Dictionary<string, string> AliasOperacion =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ACTUALIZAR"] = "CORREGIR",
            ["CAMBIAR_ESTADO"] = "CORREGIR",
            ["CONFIGURAR"] = "CORREGIR",
            ["CONFIGURAR_MODO"] = "CORREGIR",
            ["ASIGNAR"] = "ACTIVAR",
            ["ABRIR"] = "ACTIVAR",
            ["HABILITAR"] = "ACTIVAR",
            ["DESHABILITAR"] = "ANULAR",
            ["DESACTIVAR"] = "ANULAR",
            ["REACTIVAR"] = "REABRIR",
            ["VERIFICAR"] = "CERRAR"
        };

    public void Registrar(
        ClaimsPrincipal user,
        string entidad,
        string registroId,
        string operacion,
        string motivo,
        object? antes = null,
        object? despues = null,
        Guid? eventoId = null)
    {
        var operadorClaim = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(operadorClaim, out var operadorId))
        {
            throw new UnauthorizedAccessException(
                "No se pudo identificar al usuario autenticado.");
        }

        if (string.IsNullOrWhiteSpace(entidad))
            throw new ArgumentException(
                "La entidad es obligatoria.");

        if (string.IsNullOrWhiteSpace(registroId))
            throw new ArgumentException(
                "El registroId es obligatorio.");

        if (string.IsNullOrWhiteSpace(operacion))
            throw new ArgumentException(
                "La operación es obligatoria.");

        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException(
                "El motivo es obligatorio.");

        var entidadNormalizada = NormalizarEntidad(entidad);
        var operacionNormalizada = NormalizarOperacion(operacion);

        var auditoria = new Auditoria
        {
            OperadorId = operadorId,
            Entidad = entidadNormalizada,
            RegistroId = registroId,
            Operacion = operacionNormalizada,
            Motivo = motivo,
            AntesJson = antes is null
                ? null
                : JsonSerializer.Serialize(antes),
            DespuesJson = despues is null
                ? null
                : JsonSerializer.Serialize(despues),
            EventoId = eventoId,
            CreadoUtc = DateTime.UtcNow,
            SolicitudId = _requestContext.SolicitudId,
            HashSolicitud = _requestContext.HashSolicitud
        };

        _db.Auditorias.Add(auditoria);
    }

    /// <summary>
    /// Convierte el nombre de clase/entidad tal como lo pasan los servicios
    /// (PascalCase, p. ej. "SesionVisual") al nombre de tabla snake_case que
    /// exige CK_auditoria_entidad (p. ej. "sesion_visual"), o aplica un
    /// alias explícito cuando la conversión automática no coincide.
    /// </summary>
    private static string NormalizarEntidad(string entidad)
    {
        if (AliasEntidad.TryGetValue(entidad, out var alias))
            return alias;

        // Si ya viene en snake_case válido, se usa tal cual.
        if (EntidadesPermitidas.Contains(entidad))
            return entidad;

        var snake = PascalASnakeCase(entidad);

        if (!EntidadesPermitidas.Contains(snake))
        {
            throw new ArgumentException(
                $"La entidad '{entidad}' (normalizada a '{snake}') no está " +
                "permitida por CK_auditoria_entidad. Agregue un alias en " +
                "AuditService.AliasEntidad o corrija el nombre en el " +
                "servicio que llama a Registrar.");
        }

        return snake;
    }

    private static string NormalizarOperacion(string operacion)
    {
        if (OperacionesPermitidas.Contains(operacion))
            return operacion;

        if (AliasOperacion.TryGetValue(operacion, out var alias))
            return alias;

        throw new ArgumentException(
            $"La operación '{operacion}' no está permitida por " +
            "CK_auditoria_operacion. Agregue un alias en " +
            "AuditService.AliasOperacion o use uno de los valores " +
            "aprobados (CREAR, CORREGIR, ANULAR, JUSTIFICAR, PUBLICAR, " +
            "ACTIVAR, REVOCAR, CERRAR, REABRIR).");
    }

    private static string PascalASnakeCase(string value)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (char.IsUpper(c) && i > 0)
                builder.Append('_');

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
