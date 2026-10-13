namespace AulaInnova.API.DTOs.Admin.Jornada;

public class GeneracionJornadasResponse
{
    public List<JornadaResponse> JornadasCreadas { get; set; } = new();
    public int AsignacionesCreadas { get; set; }
    public List<string> Omitidas { get; set; } = new();
}
