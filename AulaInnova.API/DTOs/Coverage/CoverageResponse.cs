namespace AulaInnova.API.DTOs.Coverage;

public class CoverageResponse
{
    public long NecesidadCoberturaId { get; set; }
    public long AsignacionOrigenId { get; set; }
    public long? AsignacionCoberturaId { get; set; }
    public string Estado { get; set; } = null!;
    public DateTime CreadoUtc { get; set; }
}