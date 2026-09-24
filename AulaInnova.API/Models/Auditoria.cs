namespace AulaInnova.API.Models;

public class Auditoria
{
    public long Id { get; set; }
    public Guid OperadorId { get; set; }
    public string Entidad { get; set; } = null!;
    public string RegistroId { get; set; } = null!;
    public string Operacion { get; set; } = null!;
    public string Motivo { get; set; } = null!;
    public string? AntesJson { get; set; }
    public string? DespuesJson { get; set; }
    public Guid? EventoId { get; set; }
    public DateTime CreadoUtc { get; set; }
    public Guid SolicitudId { get; set; }
    public byte[] HashSolicitud { get; set; } = null!;

    public ApplicationUser Operador { get; set; } = null!;
    public Evento? Evento { get; set; }
}
