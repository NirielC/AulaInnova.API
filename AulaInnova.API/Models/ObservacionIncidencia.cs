namespace AulaInnova.API.Models;

public class ObservacionIncidencia
{
    public long Id { get; set; }

    public long IncidenciaId { get; set; }

    public Guid EventoId { get; set; }
    public Guid AutorId { get; set; }

    public string Texto { get; set; } = null!;

    public DateTime? HechoUtc { get; set; }

    public bool Comunicable { get; set; }
    public bool Vigente { get; set; }

    public DateTime CreadoUtc { get; set; }
    public byte[] Version { get; set; } = null!;

    public Incidencia Incidencia { get; set; } = null!;
    public Evento Evento { get; set; } = null!;
    public ApplicationUser Autor { get; set; } = null!;
}
