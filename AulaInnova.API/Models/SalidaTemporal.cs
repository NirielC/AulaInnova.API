namespace AulaInnova.API.Models;

public class SalidaTemporal
{
    public long Id { get; set; }

    public long EstudianteId { get; set; }
    public long JornadaId { get; set; }
    public long MatriculaId { get; set; }
    public long AulaId { get; set; }

    public string MotivoCodigo { get; set; } = null!;

    public Guid EventoSalidaId { get; set; }
    public Guid? EventoRegresoId { get; set; }

    public DateTime? SalidaUtc { get; set; }
    public DateTime? RegresoUtc { get; set; }
    public DateTime? ExpiraUtc { get; set; }

    public bool HorasFiables { get; set; }

    public string Estado { get; set; } = null!;
    public DateTime CreadoUtc { get; set; }
    public byte[] Version { get; set; } = null!;

    public Estudiante Estudiante { get; set; } = null!;
    public Jornada Jornada { get; set; } = null!;
    public Matricula Matricula { get; set; } = null!;
    public Aula Aula { get; set; } = null!;
    public MotivoSalida Motivo { get; set; } = null!;
    public Evento EventoSalida { get; set; } = null!;
    public Evento? EventoRegreso { get; set; }
}