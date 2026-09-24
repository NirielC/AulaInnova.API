namespace AulaInnova.API.DTOs.Incidents;

public class CreateIncidentRequest
{
    public long EstudianteId { get; set; }
    public long TipoIncidenciaId { get; set; }
    public string Motivo { get; set; } = null!;
}