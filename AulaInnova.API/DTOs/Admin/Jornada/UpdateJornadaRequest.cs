namespace AulaInnova.API.DTOs.Admin.Jornada;

public class UpdateJornadaRequest
{
    public string ZonaHoraria { get; set; } = null!;
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public DateTime LimitePuntualUtc { get; set; }
}