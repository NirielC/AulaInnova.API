namespace AulaInnova.API.DTOs.Movements;

public class MovementReturnRequest
{
    public string ReferenciaNfc { get; set; } = null!;

    // Lo pone la API con el id de la estación autenticada.
    public long EstacionId { get; set; }

    public DateTime? OcurridoUtc { get; set; }
    public Guid? SeleccionId { get; set; }
    public int? SeleccionEdadMs { get; set; }
}
