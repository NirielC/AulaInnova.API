using AulaInnova.API.Data;
using AulaInnova.API.Models;
using AulaInnova.API.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AulaInnova.API.Services;

/// <summary>
/// Reglas comunes a todo lo que registra una estación física
/// (asistencia, salidas/regresos y presencia del personal).
/// Cada regla que no se cumple lanza <see cref="RechazoEstacionException"/>
/// con un código fijo que la estación traduce a un mensaje en su pantalla.
/// </summary>
public static class EstacionReglas
{
    // Tolerancia para una hora declarada (registro de la cola offline)
    // que llega un poco "del futuro" por diferencia entre relojes.
    private static readonly TimeSpan MargenHoraFutura = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Sin ocurridoUtc la hora es la del servidor (FIABLE). Con ocurridoUtc
    /// (registro guardado sin red) debe venir en UTC y no estar en el futuro.
    /// </summary>
    public static DateTime HoraDeLectura(DateTime? ocurridoUtc)
    {
        if (!ocurridoUtc.HasValue)
            return DateTime.UtcNow;

        var valor = ocurridoUtc.Value;

        if (valor.Kind != DateTimeKind.Utc)
            throw new RechazoEstacionException(
                "HORA_NO_UTC",
                "ocurridoUtc debe venir en UTC (terminar en Z).");

        if (valor > DateTime.UtcNow + MargenHoraFutura)
            throw new RechazoEstacionException(
                "HORA_FUTURA",
                "La hora declarada está en el futuro: revise el reloj de la estación.");

        return valor;
    }

    public static string CalidadHora(DateTime? ocurridoUtc) =>
        ocurridoUtc.HasValue ? "DECLARADA" : "FIABLE";

    /// <summary>
    /// ai.credencial_nfc solo admite hexadecimal en mayúsculas
    /// (CK_credencial_nfc_referencia): se quitan espacios y guiones.
    /// </summary>
    public static string NormalizarReferencia(string? referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia))
            throw new RechazoEstacionException(
                "REFERENCIA_INVALIDA",
                "Falta la referencia NFC.");

        return referencia
            .Trim()
            .Replace("-", "")
            .Replace(":", "")
            .ToUpperInvariant();
    }

    /// <summary>
    /// Versión para el alta de credenciales (AdminNfcService): normaliza
    /// igual que <see cref="NormalizarReferencia"/> y además valida que
    /// cumpla CK_credencial_nfc_referencia (solo 0-9 y A-F).
    /// </summary>
    public static string NormalizarReferenciaAdmin(string? referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia))
            throw new ArgumentException("La referencia NFC es obligatoria.");

        var limpia = referencia
            .Trim()
            .Replace("-", "")
            .Replace(":", "")
            .Replace(" ", "")
            .ToUpperInvariant();

        if (limpia.Length == 0 ||
            limpia.Length > 64 ||
            limpia.Any(c => !Uri.IsHexDigit(c)))
        {
            throw new ArgumentException(
                "La referencia NFC solo puede tener números y letras A-F " +
                "(por ejemplo el UUID de la credencial sin guiones).");
        }

        return limpia;
    }

    public static async Task<EstacionConfiguracion> ConfiguracionVigenteAsync(
        AulaInnovaDbContext db,
        long estacionId,
        DateTime utc)
    {
        var configuracion = await ConfiguracionEnAsync(db, estacionId, utc);

        // Un registro de la cola offline puede ser de antes de la
        // configuración actual (la estación se reconfiguró mientras no
        // tenía red). Se usa la vigente para no perder el registro.
        if (configuracion is null && utc < DateTime.UtcNow)
            configuracion = await ConfiguracionEnAsync(db, estacionId, DateTime.UtcNow);

        return configuracion ?? throw new RechazoEstacionException(
            "ESTACION_SIN_CONFIGURACION",
            "La estación no tiene una configuración vigente para esa hora.");
    }

    private static Task<EstacionConfiguracion?> ConfiguracionEnAsync(
        AulaInnovaDbContext db,
        long estacionId,
        DateTime utc)
    {
        return db.EstacionesConfiguracion
            .FirstOrDefaultAsync(c =>
                c.EstacionId == estacionId &&
                c.VigenteDesdeUtc <= utc &&
                (c.VigenteHastaUtc == null ||
                 c.VigenteHastaUtc > utc));
    }

    /// <summary>
    /// La acción debe estar en ai.estacion_operacion para esa configuración
    /// (además, FK_evento_configuracion_operacion lo exige en la base).
    /// </summary>
    public static async Task ExigirOperacionAsync(
        AulaInnovaDbContext db,
        long configuracionId,
        string accion)
    {
        var habilitada = await db.EstacionesOperaciones
            .AnyAsync(o =>
                o.EstacionConfiguracionId == configuracionId &&
                o.Accion == accion);

        if (!habilitada)
            throw new RechazoEstacionException(
                "OPERACION_NO_HABILITADA",
                $"La estación no tiene habilitada la operación {accion}.");
    }

    public static async Task<CredencialNfc> CredencialAsync(
        AulaInnovaDbContext db,
        string? referenciaNfc,
        DateTime utc)
    {
        var referencia = NormalizarReferencia(referenciaNfc);

        var credencial = await db.CredencialesNfc
            .Include(c => c.Persona)
            .FirstOrDefaultAsync(c =>
                c.Referencia == referencia &&
                c.Activa &&
                c.DesdeUtc <= utc &&
                (c.HastaUtc == null || c.HastaUtc > utc));

        if (credencial is null)
            throw new RechazoEstacionException(
                "CREDENCIAL_NO_REGISTRADA",
                "La tarjeta no está registrada o no está activa.");

        if (!credencial.Persona.Habilitada)
            throw new RechazoEstacionException(
                "PERSONA_DESHABILITADA",
                "La persona de esta tarjeta está deshabilitada.");

        return credencial;
    }

    public static Task<Jornada?> JornadaDeAsync(
        AulaInnovaDbContext db,
        DateTime utc)
    {
        return db.Jornadas
            .FirstOrDefaultAsync(j =>
                j.InicioUtc <= utc &&
                j.FinUtc >= utc &&
                j.Estado != "CANCELADA");
    }

    public static async Task<Jornada> JornadaObligatoriaAsync(
        AulaInnovaDbContext db,
        DateTime utc)
    {
        return await JornadaDeAsync(db, utc)
            ?? throw new RechazoEstacionException(
                "SIN_JORNADA",
                "No hay una jornada escolar a esta hora.");
    }

    public static async Task<Estudiante> EstudianteAsync(
        AulaInnovaDbContext db,
        long personaId)
    {
        return await db.Estudiantes
            .FirstOrDefaultAsync(e => e.PersonaId == personaId)
            ?? throw new RechazoEstacionException(
                "NO_ES_ESTUDIANTE",
                "La tarjeta no es de un estudiante.");
    }

    public static async Task<Matricula> MatriculaAsync(
        AulaInnovaDbContext db,
        long estudianteId,
        Jornada jornada)
    {
        return await db.Matriculas
            .FirstOrDefaultAsync(m =>
                m.EstudianteId == estudianteId &&
                !m.Anulada &&
                m.Desde <= jornada.FechaLocal &&
                (m.Hasta == null || m.Hasta >= jornada.FechaLocal))
            ?? throw new RechazoEstacionException(
                "SIN_MATRICULA",
                "El estudiante no tiene matrícula vigente.");
    }

    /// <summary>
    /// seleccion_id identifica UNA operación hecha en la estación (elegir
    /// motivo + pasar la tarjeta). UX_evento_estacion_seleccion lo hace
    /// único por estación, así que si la cola offline reenvía lo mismo,
    /// la API lo reconoce y no duplica el registro.
    /// Clientes viejos que no lo mandan reciben uno nuevo.
    /// </summary>
    public static (Guid Id, int EdadMs) Seleccion(
        Guid? seleccionId,
        int? seleccionEdadMs)
    {
        if (seleccionEdadMs is < 0)
            throw new RechazoEstacionException(
                "SELECCION_INVALIDA",
                "seleccionEdadMs no puede ser negativo.");

        var id = seleccionId is null || seleccionId == Guid.Empty
            ? Guid.NewGuid()
            : seleccionId.Value;

        return (id, seleccionEdadMs ?? 0);
    }

    public static Task<Evento?> EventoPorSeleccionAsync(
        AulaInnovaDbContext db,
        long estacionId,
        Guid seleccionId)
    {
        return db.Eventos
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.EstacionId == estacionId &&
                e.SeleccionId == seleccionId);
    }

    public static byte[] Hash(string contenido) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(contenido));

    /// <summary>
    /// Rol de la persona para la estación: ESTUDIANTE, ADMINISTRADOR,
    /// PROFESOR, AUXILIAR o PERSONAL (personal sin usuario del sistema).
    /// Un usuario bloqueado no cuenta como administrador.
    /// </summary>
    public static async Task<string> TipoPersonaAsync(
        AulaInnovaDbContext db,
        Persona persona)
    {
        if (await db.Estudiantes.AnyAsync(e => e.PersonaId == persona.Id))
            return "ESTUDIANTE";

        var ahora = DateTimeOffset.UtcNow;

        var roles = await (
                from u in db.Users
                where u.PersonaId == persona.Id &&
                      (u.LockoutEnd == null || u.LockoutEnd <= ahora)
                join ur in db.UserRoles on u.Id equals ur.UserId
                join r in db.Roles on ur.RoleId equals r.Id
                select r.Name)
            .ToListAsync();

        if (roles.Contains("Administrador")) return "ADMINISTRADOR";
        if (roles.Contains("Profesor")) return "PROFESOR";
        if (roles.Contains("Auxiliar")) return "AUXILIAR";

        return "PERSONAL";
    }

    /// <summary>
    /// Nombre para la LCD de 16 caracteres: sin tildes ni ñ (la pantalla
    /// no las tiene). "Elias Lopez" o, si no cabe, "Elias L.".
    /// </summary>
    public static string NombreCorto(Persona persona)
    {
        var nombre = PrimeraPalabra(persona.Nombres);
        var apellido = PrimeraPalabra(persona.Apellidos);

        var texto = $"{nombre} {apellido}".Trim();

        if (texto.Length > 16 && apellido.Length > 0)
            texto = $"{nombre} {apellido[0]}.";

        texto = SoloAscii(texto);

        return texto.Length > 16 ? texto[..16] : texto;
    }

    public static string SoloAscii(string texto)
    {
        var builder = new StringBuilder();

        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) ==
                UnicodeCategory.NonSpacingMark)
                continue;

            if (c >= 32 && c <= 126)
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static string PrimeraPalabra(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return "";

        return texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }
}
