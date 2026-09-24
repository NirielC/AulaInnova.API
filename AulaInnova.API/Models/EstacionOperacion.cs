namespace AulaInnova.API.Models;

public class EstacionOperacion
{
    public long EstacionConfiguracionId { get; set; }
    public string Accion { get; set; } = null!;
    public string ModoInterfaz { get; set; } = null!;

    public EstacionConfiguracion EstacionConfiguracion { get; set; } = null!;
}
