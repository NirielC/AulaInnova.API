using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using AulaInnova.API.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AulaInnova.API.Authentication;

/// <summary>
/// Esquema de autenticación separado del JWT de Identity, pensado para
/// dispositivos (estaciones NFC), no para personas.
///
/// La estación debe enviar dos encabezados:
///   X-Estacion-Id     : el id numérico de ai.estacion
///   X-Estacion-Clave  : "{identificador_clave}.{secreto_en_claro}"
///                       (el valor completo que devolvió
///                       POST /api/admin/estaciones/{id}/secret)
///
/// Se valida contra ai.estacion_secreto: el secreto no debe estar
/// revocado y su hash SHA-256 debe coincidir con hash_secreto. Ningún
/// JWT de usuario, sea cual sea su rol, pasa por este esquema.
/// </summary>
public class EstacionAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Estacion";

    private const string HeaderEstacionId = "X-Estacion-Id";
    private const string HeaderEstacionClave = "X-Estacion-Clave";

    private readonly AulaInnovaDbContext _db;

    public EstacionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AulaInnovaDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderEstacionId, out var idValues) ||
            !Request.Headers.TryGetValue(HeaderEstacionClave, out var claveValues))
        {
            return AuthenticateResult.Fail(
                $"Faltan los encabezados {HeaderEstacionId}/{HeaderEstacionClave}.");
        }

        if (!long.TryParse(idValues.ToString(), out var estacionId))
            return AuthenticateResult.Fail("X-Estacion-Id inválido.");

        var claveCompleta = claveValues.ToString();
        var separador = claveCompleta.IndexOf('.');

        if (separador <= 0 || separador == claveCompleta.Length - 1)
            return AuthenticateResult.Fail("X-Estacion-Clave con formato inválido.");

        var identificadorClave = claveCompleta[..separador];
        var secretoPlano = claveCompleta[(separador + 1)..];

        var utcNow = DateTime.UtcNow;

        var secreto = await _db.EstacionesSecretos
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.EstacionId == estacionId &&
                s.IdentificadorClave == identificadorClave &&
                s.RevocadaUtc == null &&
                s.DesdeUtc <= utcNow);

        if (secreto is null)
            return AuthenticateResult.Fail("Secreto de estación no reconocido o revocado.");

        var estacionHabilitada = await _db.Estaciones
            .AsNoTracking()
            .AnyAsync(e => e.Id == estacionId && e.Habilitada);

        if (!estacionHabilitada)
            return AuthenticateResult.Fail("La estación no está habilitada.");

        var hashRecibido = SHA256.HashData(
            Encoding.UTF8.GetBytes(secretoPlano));

        if (!CryptographicOperations.FixedTimeEquals(
                hashRecibido,
                secreto.HashSecreto))
        {
            return AuthenticateResult.Fail("Secreto de estación incorrecto.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Estacion"),
            new("estacion_id", estacionId.ToString())
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
