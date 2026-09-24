namespace AulaInnova.API.Models;

public class Asignacion
{
    public long Id { get; set; }

    public long JornadaId { get; set; }
    public long GrupoId { get; set; }
    public long? HorarioClaseId { get; set; }
    public long PersonalId { get; set; }
    public long AulaId { get; set; }

    public string Tipo { get; set; } = null!;

    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }

    public string Estado { get; set; } = null!;

    public long? SustituyeId { get; set; }

    public string? Actividad { get; set; }

    public DateTime CreadoUtc { get; set; }
    public byte[] Version { get; set; } = null!;

    public Jornada Jornada { get; set; } = null!;
    public Grupo Grupo { get; set; } = null!;
    public HorarioClase? HorarioClase { get; set; }
    public Personal Personal { get; set; } = null!;
    public Aula Aula { get; set; } = null!;

    public Asignacion? Sustituye { get; set; }
    public ICollection<Asignacion> Sustituciones { get; set; } =
        new List<Asignacion>();
}
