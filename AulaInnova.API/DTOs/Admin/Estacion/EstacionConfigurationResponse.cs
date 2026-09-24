namespace AulaInnova.API.DTOs.Admin.Estacion;

public class EstacionConfigurationResponse
{
    public long Id { get; set; }
    public long EstacionId { get; set; }
    public string TipoPunto { get; set; } = null!;
    public long? AulaId { get; set; }
    public string EtiquetaPunto { get; set; } = null!;
    public short ExpiracionSelectorS { get; set; }
    public DateTime VigenteDesdeUtc { get; set; }
    public DateTime? VigenteHastaUtc { get; set; }
    public Guid ConfiguradaPor { get; set; }
    public string MotivoCambio { get; set; } = null!;
}