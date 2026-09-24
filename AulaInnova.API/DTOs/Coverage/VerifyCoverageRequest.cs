namespace AulaInnova.API.DTOs.Coverage;

public class VerifyCoverageRequest
{
    public long AsignacionId { get; set; }
    public string Estado { get; set; } = null!;
    public string Observacion { get; set; } = null!;
    public DateTime? OcurridoUtc { get; set; }
}