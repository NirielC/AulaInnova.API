namespace AulaInnova.API.Models;

public class Evento
{
    public Guid Id { get; set; }
    public byte[] HashSolicitud { get; set; } = null!;
    public string Origen { get; set; } = null!;
    public string Accion { get; set; } = null!;

    public long? PersonaId { get; set; }
    public long? CredencialId { get; set; }
    public long? EstacionId { get; set; }
    public long? EstacionConfiguracionId { get; set; }
    public Guid? OperadorId { get; set; }
    public long? AulaId { get; set; }
    public long? JornadaId { get; set; }
    public long? MatriculaId { get; set; }
    public long? AsignacionId { get; set; }

    public string? PuntoManual { get; set; }
    public string? MotivoCodigo { get; set; }
    public string? MotivoManual { get; set; }

    public DateTime? OcurridoUtc { get; set; }
    public DateTime RecibidoUtc { get; set; }

    public string CalidadHora { get; set; } = null!;
    public string Estado { get; set; } = null!;
    public string CodigoResultado { get; set; } = null!;

    public Guid? EventoCanonicoId { get; set; }
    public Guid? SeleccionId { get; set; }
    public int? SeleccionEdadMs { get; set; }

    public Persona? Persona { get; set; }
    public CredencialNfc? Credencial { get; set; }
    public Estacion? Estacion { get; set; }
    public EstacionConfiguracion? EstacionConfiguracion { get; set; }
    public ApplicationUser? Operador { get; set; }
    public Aula? Aula { get; set; }
    public Jornada? Jornada { get; set; }
    public Matricula? Matricula { get; set; }
    public Evento? EventoCanonico { get; set; }
    public Asignacion? Asignacion { get; set; }
}
