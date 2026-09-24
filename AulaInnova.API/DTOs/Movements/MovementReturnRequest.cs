namespace AulaInnova.API.DTOs.Movements;

public class MovementReturnRequest
{
    public string ReferenciaNfc { get; set; } = null!;
    public long EstacionId { get; set; }
    public DateTime? OcurridoUtc { get; set; }
}