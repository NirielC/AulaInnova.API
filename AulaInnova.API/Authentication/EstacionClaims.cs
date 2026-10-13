using System.Security.Claims;

namespace AulaInnova.API.Authentication;

public static class EstacionClaims
{
    /// <summary>
    /// Id de la estación autenticada (claim "estacion_id" que pone
    /// EstacionAuthenticationHandler). Las rutas de estación usan este
    /// valor y no el que venga en el cuerpo, para que una estación no
    /// pueda registrar a nombre de otra.
    /// </summary>
    public static long EstacionId(this ClaimsPrincipal user)
    {
        var valor = user.FindFirstValue("estacion_id");

        return long.TryParse(valor, out var id)
            ? id
            : throw new UnauthorizedAccessException(
                "La solicitud no viene de una estación autenticada.");
    }
}
