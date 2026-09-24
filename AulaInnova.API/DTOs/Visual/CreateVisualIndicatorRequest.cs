namespace AulaInnova.API.DTOs.Visual;

public class CreateVisualIndicatorRequest
{
    public string Codigo { get; set; } = null!;
    public int Revision { get; set; }
    public string Unidad { get; set; } = null!;
    public string Metodo { get; set; } = null!;
    public string ConfiguracionJson { get; set; } = null!;
    public string ReferenciaValidacion { get; set; } = null!;
}