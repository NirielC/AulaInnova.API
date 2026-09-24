namespace AulaInnova.API.Models;

public class Asistencia
{
    public long Id { get; set; }
    public long EstudianteId { get; set; }
    public long JornadaId { get; set; }
    public long MatriculaId { get; set; }
    public Guid? EventoLlegadaId { get; set; }
    public DateTime? LlegadaUtc { get; set; }
    public string CalidadHora { get; set; } = null!;
    public string Estado { get; set; } = null!;
    public string Puntualidad { get; set; } = null!;
    public string? Justificacion { get; set; }
    public Guid? RevisadoPor { get; set; }
    public DateTime? RevisadoUtc { get; set; }
    public DateTime CreadoUtc { get; set; }
    public byte[] Version { get; set; } = null!;

    public Estudiante Estudiante { get; set; } = null!;
    public Jornada Jornada { get; set; } = null!;
    public Matricula Matricula { get; set; } = null!;
    public Evento? EventoLlegada { get; set; }
    public ApplicationUser? Revisor { get; set; }
}
