namespace AulaInnova.API.DTOs.Movements;

public class MovementExitRequest
{
    public string ReferenciaNfc { get; set; } = null!;
    public long EstacionId { get; set; }
    public string MotivoCodigo { get; set; } = null!;
    public DateTime? OcurridoUtc { get; set; }
}