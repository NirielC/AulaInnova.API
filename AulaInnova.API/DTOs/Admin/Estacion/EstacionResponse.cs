namespace AulaInnova.API.DTOs.Admin.Estacion;

public class EstacionResponse
{
    public long Id { get; set; }
    public string CodigoLogico { get; set; } = null!;
    public bool Habilitada { get; set; }
    public string? MacInventario { get; set; }
    public DateTime? UltimaComunicacionUtc { get; set; }
    public string? ModoReportado { get; set; }
}