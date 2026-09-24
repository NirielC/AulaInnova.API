namespace AulaInnova.API.Models;

public class EstacionSecreto
{
    public long Id { get; set; }
    public long EstacionId { get; set; }
    public string IdentificadorClave { get; set; } = null!;
    public byte[] HashSecreto { get; set; } = null!;
    public DateTime DesdeUtc { get; set; }
    public DateTime? RevocadaUtc { get; set; }

    public Estacion Estacion { get; set; } = null!;
}
