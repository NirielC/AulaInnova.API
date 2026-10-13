namespace AulaInnova.API.DTOs.Estacion;

/// <summary>
/// Una tarjeta pasada por la estación. La API decide qué registrar según
/// quién es la persona:
///   - personal (profesor, auxiliar, admin): llegada/salida al aula;
///   - estudiante en modo ASISTENCIA: asistencia del día;
///   - estudiante en modo MOVIMIENTOS: no registra nada todavía, responde
///     si debe elegir motivo de salida o confirmar su regreso.
/// </summary>
public class LecturaEstacionRequest
{
    public string ReferenciaNfc { get; set; } = null!;

    // ASISTENCIA o MOVIMIENTOS: el modo que muestra la estación.
    public string Modo { get; set; } = null!;

    // Solo en registros guardados sin red (cola offline).
    public DateTime? OcurridoUtc { get; set; }

    // Id de esta lectura en la estación (evita duplicados al reenviar).
    public Guid? SeleccionId { get; set; }
    public int? SeleccionEdadMs { get; set; }
}
