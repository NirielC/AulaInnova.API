namespace AulaInnova.API.DTOs.Movements;

public class MovementExitRequest
{
    public string ReferenciaNfc { get; set; } = null!;

    // Lo pone la API con el id de la estación autenticada
    // (X-Estacion-Id); el valor que venga en el cuerpo se ignora.
    public long EstacionId { get; set; }

    public string MotivoCodigo { get; set; } = null!;

    // Solo en registros guardados sin red (cola offline).
    public DateTime? OcurridoUtc { get; set; }

    // Id de esta operación en la estación: si se reenvía igual, la API no
    // la duplica (UX_evento_estacion_seleccion).
    public Guid? SeleccionId { get; set; }

    // Milisegundos entre la lectura de la tarjeta y la confirmación.
    public int? SeleccionEdadMs { get; set; }
}
