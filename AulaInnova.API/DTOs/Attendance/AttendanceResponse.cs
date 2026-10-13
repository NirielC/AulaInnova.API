namespace AulaInnova.API.DTOs.Attendance;

public class AttendanceResponse
{
    public long AsistenciaId { get; set; }
    public string Estado { get; set; } = null!;
    public string Puntualidad { get; set; } = null!;
    public DateTime? LlegadaUtc { get; set; }

    // true si el estudiante ya tenía asistencia en esta jornada
    // (pasó la tarjeta otra vez): no se creó un registro nuevo.
    public bool YaRegistrada { get; set; }
}
