namespace AulaInnova.API.Models;

public class Incidencia
{
    public long Id { get; set; }

    public long EstudianteId { get; set; }
    public long JornadaId { get; set; }
    public long TipoIncidenciaId { get; set; }

    public Guid EventoOrigenId { get; set; }
    public Guid CreadaPor { get; set; }

    public bool Vigente { get; set; }

    public DateTime CreadoUtc { get; set; }
    public byte[] Version { get; set; } = null!;

    public Estudiante Estudiante { get; set; } = null!;
    public Jornada Jornada { get; set; } = null!;
    public TipoIncidencia TipoIncidencia { get; set; } = null!;
    public Evento EventoOrigen { get; set; } = null!;
    public ApplicationUser Creador { get; set; } = null!;

    public ICollection<ObservacionIncidencia> Observaciones { get; set; } =
        new List<ObservacionIncidencia>();
}
