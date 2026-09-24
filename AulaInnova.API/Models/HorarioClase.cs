namespace AulaInnova.API.Models;

public class HorarioClase
{
    public long Id { get; set; }
    public long AsignaturaId { get; set; }
    public long GrupoId { get; set; }
    public long DocenteId { get; set; }
    public long AulaId { get; set; }
    public byte DiaSemana { get; set; }
    public DateOnly VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public bool Anulado { get; set; }
    public DateTime CreadoUtc { get; set; }
    public byte[] Version { get; set; } = null!;

    public Asignatura Asignatura { get; set; } = null!;
    public Grupo Grupo { get; set; } = null!;
    public Personal Docente { get; set; } = null!;
    public Aula Aula { get; set; } = null!;
}
