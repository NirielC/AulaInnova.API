namespace AulaInnova.API.DTOs.Admin.Estacion;

public class EstacionSecretResponse
{
    public long EstacionId { get; set; }
    public string IdentificadorClave { get; set; } = null!;

    /// <summary>
    /// Único momento en que el secreto en claro es visible. No se guarda
    /// en ai.estacion_secreto (solo su hash SHA-256) ni se puede volver
    /// a consultar después de esta respuesta.
    /// </summary>
    public string Secreto { get; set; } = null!;

    public DateTime DesdeUtc { get; set; }
}
