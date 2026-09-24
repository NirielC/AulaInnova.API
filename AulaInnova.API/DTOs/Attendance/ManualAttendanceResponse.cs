namespace AulaInnova.API.DTOs.Attendance;

public class ManualAttendanceResponse
{
    public long AsistenciaId { get; set; }
    public Guid EventoId { get; set; }
    public string Estado { get; set; } = null!;
    public string Puntualidad { get; set; } = null!;
    public DateTime LlegadaUtc { get; set; }
}