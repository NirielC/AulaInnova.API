namespace AulaInnova.API.Models;

public class FallaEstacion
{
    public long Id { get; set; }

    public long EstacionId { get; set; }
    public Guid ReportadoPor { get; set; }

    public string Descripcion { get; set; } = null!;
    public string Estado { get; set; } = null!;

    public DateTime? ResueltaUtc { get; set; }
    public DateTime CreadoUtc { get; set; }

    public byte[] Version { get; set; } = null!;

    public Estacion Estacion { get; set; } = null!;
    public ApplicationUser Reportador { get; set; } = null!;
}
