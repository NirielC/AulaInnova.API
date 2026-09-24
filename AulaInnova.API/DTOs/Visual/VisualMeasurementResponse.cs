namespace AulaInnova.API.DTOs.Visual;

public class VisualMeasurementResponse
{
    public long Id { get; set; }
    public long SesionId { get; set; }
    public long ZonaId { get; set; }
    public string ZonaCodigo { get; set; } = null!;
    public long IndicadorId { get; set; }
    public string IndicadorCodigo { get; set; } = null!;
    public string IndicadorUnidad { get; set; } = null!;
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public decimal? Valor { get; set; }
    public bool Valida { get; set; }
    public string? MotivoInvalidez { get; set; }
    public int MuestrasValidas { get; set; }
    public int MuestrasTotales { get; set; }
}
