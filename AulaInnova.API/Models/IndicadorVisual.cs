namespace AulaInnova.API.Models;

public class IndicadorVisual
{
    public long Id { get; set; }
    public string Codigo { get; set; } = null!;
    public int Revision { get; set; }
    public string Unidad { get; set; } = null!;
    public string Metodo { get; set; } = null!;
    public string ConfiguracionJson { get; set; } = null!;
    public string ReferenciaValidacion { get; set; } = null!;
    public bool Habilitado { get; set; }
}
