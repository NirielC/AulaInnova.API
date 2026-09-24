namespace AulaInnova.API.Models;

public class MedicionVisual
{
    public long Id { get; set; }
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
    public DateTime CreadoUtc { get; set; }

    public SesionVisual Sesion { get; set; } = null!;
    public ZonaVisual Zona { get; set; } = null!;
    public IndicadorVisual Indicador { get; set; } = null!;
}
