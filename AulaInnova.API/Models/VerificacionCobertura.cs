namespace AulaInnova.API.Models;

public class VerificacionCobertura
{
    public long Id { get; set; }

    public long AsignacionId { get; set; }
    public Guid VerificadoPor { get; set; }

    public string Estado { get; set; } = null!;
    public string Observacion { get; set; } = null!;

    public DateTime OcurridoUtc { get; set; }
    public DateTime CreadoUtc { get; set; }

    public Asignacion Asignacion { get; set; } = null!;
    public ApplicationUser Verificador { get; set; } = null!;
}
