namespace AulaInnova.API.DTOs.Movements;

public class MovementResponse
{
    public long SalidaTemporalId { get; set; }
    public string Accion { get; set; } = null!;
    public string Estado { get; set; } = null!;
    public DateTime? SalidaUtc { get; set; }
    public DateTime? RegresoUtc { get; set; }
    public DateTime? ExpiraUtc { get; set; }
}