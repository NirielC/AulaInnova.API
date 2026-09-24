namespace AulaInnova.API.DTOs.Visual;

public class OpenVisualSessionRequest
{
    public long AsignacionId { get; set; }
    public long AulaId { get; set; }
    public string ReferenciaAutorizacion { get; set; } = null!;
    public string VersionProceso { get; set; } = null!;
    public DateTime InicioUtc { get; set; }
}