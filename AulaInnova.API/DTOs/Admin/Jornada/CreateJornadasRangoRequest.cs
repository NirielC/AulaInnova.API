namespace AulaInnova.API.DTOs.Admin.Jornada;

/// <summary>
/// Crea de una vez las jornadas de un periodo (por ejemplo el mes o el
/// trimestre), con el mismo horario cada día lectivo. Las fechas que ya
/// tienen jornada se saltan. Las horas son locales (de ZonaHoraria).
/// </summary>
public class CreateJornadasRangoRequest
{
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
    public TimeOnly HoraLimitePuntual { get; set; }
    public string ZonaHoraria { get; set; } = "America/El_Salvador";

    // 1 = lunes ... 7 = domingo. Por defecto, de lunes a viernes.
    public List<byte> Dias { get; set; } = new() { 1, 2, 3, 4, 5 };
}
