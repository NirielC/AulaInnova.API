namespace AulaInnova.API.DTOs.Visual;

public class RecordVisualMeasurementRequest
{
    public long SesionId { get; set; }
    public long ZonaId { get; set; }
    public long IndicadorId { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public decimal? Valor { get; set; }
    public bool Valida { get; set; }
    public string? MotivoInvalidez { get; set; }
    public int MuestrasValidas { get; set; }
    public int MuestrasTotales { get; set; }
}