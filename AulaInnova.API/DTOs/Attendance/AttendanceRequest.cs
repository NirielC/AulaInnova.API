namespace AulaInnova.API.DTOs.Attendance;

public class AttendanceRequest
{
    public string ReferenciaNfc { get; set; } = null!;
    public long EstacionId { get; set; }
    public DateTime? OcurridoUtc { get; set; }
}