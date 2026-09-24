namespace AulaInnova.API.DTOs.Admin.Jornada;

public class JornadaResponse
{
    public long Id { get; set; }
    public DateOnly FechaLocal { get; set; }
    public string ZonaHoraria { get; set; } = null!;
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public DateTime LimitePuntualUtc { get; set; }
    public string Estado { get; set; } = null!;
}