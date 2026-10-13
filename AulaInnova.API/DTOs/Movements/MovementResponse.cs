namespace AulaInnova.API.DTOs.Movements;

public class MovementResponse
{
    public long SalidaTemporalId { get; set; }
    public string Accion { get; set; } = null!;
    public string Estado { get; set; } = null!;
    public string MotivoCodigo { get; set; } = null!;
    public DateTime? SalidaUtc { get; set; }
    public DateTime? RegresoUtc { get; set; }
    public DateTime? ExpiraUtc { get; set; }

    // true si era un reenvío de una selección ya guardada.
    public bool Repetida { get; set; }
}
