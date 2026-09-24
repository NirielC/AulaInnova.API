namespace AulaInnova.API.Models;

public class SesionVisual
{
    public long Id { get; set; }
    public long AsignacionId { get; set; }
    public long AulaId { get; set; }
    public Guid AutorizadaPor { get; set; }
    public string ReferenciaAutorizacion { get; set; } = null!;
    public DateTime InicioUtc { get; set; }
    public DateTime? FinUtc { get; set; }
    public string VersionProceso { get; set; } = null!;
    public string Estado { get; set; } = null!;

    public Asignacion Asignacion { get; set; } = null!;
    public Aula Aula { get; set; } = null!;
    public ApplicationUser Autorizador { get; set; } = null!;
}
