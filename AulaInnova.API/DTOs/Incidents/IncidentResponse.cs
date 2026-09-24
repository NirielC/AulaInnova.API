namespace AulaInnova.API.DTOs.Incidents;

public class IncidentResponse
{
    public long IncidenciaId { get; set; }
    public long EstudianteId { get; set; }
    public long JornadaId { get; set; }
    public long TipoIncidenciaId { get; set; }
    public bool Vigente { get; set; }
    public DateTime CreadoUtc { get; set; }
}