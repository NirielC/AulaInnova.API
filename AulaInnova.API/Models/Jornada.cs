namespace AulaInnova.API.Models;

public class Jornada
{
    public long Id { get; set; }
    public DateOnly FechaLocal { get; set; }
    public string ZonaHoraria { get; set; } = null!;
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public DateTime LimitePuntualUtc { get; set; }
    public string Estado { get; set; } = null!;
    public string? MotivoExcepcion { get; set; }
    public byte[] Version { get; set; } = null!;
}
