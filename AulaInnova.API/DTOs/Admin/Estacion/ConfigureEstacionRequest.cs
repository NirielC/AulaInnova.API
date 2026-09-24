namespace AulaInnova.API.DTOs.Admin.Estacion;

public class ConfigureEstacionRequest
{
    public string TipoPunto { get; set; } = null!;
    public long? AulaId { get; set; }
    public string EtiquetaPunto { get; set; } = null!;
    public short ExpiracionSelectorS { get; set; }
    public string MotivoCambio { get; set; } = null!;
    public bool PermiteAsistencia { get; set; }
    public bool PermiteMovimientos { get; set; }
}