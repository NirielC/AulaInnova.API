namespace AulaInnova.API.Models;

public class NecesidadCobertura
{
    public long Id { get; set; }

    public long AsignacionOrigenId { get; set; }
    public long? AsignacionCoberturaId { get; set; }

    public string Estado { get; set; } = null!;
    public string Motivo { get; set; } = null!;

    public Guid CreadaPor { get; set; }
    public DateTime CreadoUtc { get; set; }

    public Guid? CerradoPor { get; set; }
    public DateTime? CerradoUtc { get; set; }

    public byte[] Version { get; set; } = null!;

    public Asignacion AsignacionOrigen { get; set; } = null!;
    public Asignacion? AsignacionCobertura { get; set; }

    public ApplicationUser Creador { get; set; } = null!;
    public ApplicationUser? Cerrador { get; set; }
}
