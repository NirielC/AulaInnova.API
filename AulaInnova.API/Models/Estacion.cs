namespace AulaInnova.API.Models;

public class Estacion
{
    public long Id { get; set; }
    public string CodigoLogico { get; set; } = null!;
    public bool Habilitada { get; set; }
    public string? MacInventario { get; set; }
    public DateTime? UltimaComunicacionUtc { get; set; }
    public string? ModoReportado { get; set; }
    public DateTime? ModoReportadoUtc { get; set; }
    public DateTime CreadoUtc { get; set; }
    public byte[] Version { get; set; } = null!;

    public ICollection<EstacionConfiguracion> Configuraciones { get; set; }
        = new List<EstacionConfiguracion>();

    public ICollection<EstacionSecreto> Secretos { get; set; }
        = new List<EstacionSecreto>();
}
