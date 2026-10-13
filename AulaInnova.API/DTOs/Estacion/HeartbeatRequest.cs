namespace AulaInnova.API.DTOs.Estacion;

public class HeartbeatRequest
{
    // ASISTENCIA o MOVIMIENTOS (lo que muestra la estación ahora).
    public string? Modo { get; set; }

    // Registros guardados en la microSD esperando red (solo informativo).
    public int? Pendientes { get; set; }
}

public class HeartbeatResponse
{
    public DateTime ServidorUtc { get; set; }
}
