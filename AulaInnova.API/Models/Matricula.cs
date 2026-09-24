namespace AulaInnova.API.Models;

public class Matricula
{
    public long Id { get; set; }

    public long EstudianteId { get; set; }

    public long GrupoId { get; set; }

    public DateOnly Desde { get; set; }

    public DateOnly? Hasta { get; set; }

    public bool Anulada { get; set; }

    public DateTime CreadoUtc { get; set; }

    public byte[] Version { get; set; } = null!;

    public Estudiante Estudiante { get; set; } = null!;

    public Grupo Grupo { get; set; } = null!;
}