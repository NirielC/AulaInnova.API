namespace AulaInnova.API.DTOs.Attendance;

public class ManualAttendanceRequest
{
    public long EstudianteId { get; set; }
    public long? JornadaId { get; set; }
    public DateTime? OcurridoUtc { get; set; }
    public string PuntoManual { get; set; } = null!;
    public string MotivoManual { get; set; } = null!;
}