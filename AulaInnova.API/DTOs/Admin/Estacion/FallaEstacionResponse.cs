namespace AulaInnova.API.DTOs.Admin.Estacion;

public class FallaEstacionResponse
{
    public long Id { get; set; }
    public long EstacionId { get; set; }
    public Guid ReportadoPor { get; set; }
    public string Descripcion { get; set; } = null!;
    public string Estado { get; set; } = null!;
    public DateTime? ResueltaUtc { get; set; }
    public DateTime CreadoUtc { get; set; }
}