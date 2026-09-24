namespace AulaInnova.API.DTOs.Incidents;

public class AddIncidentObservationRequest
{
    public long IncidenciaId { get; set; }
    public string Texto { get; set; } = null!;
    public DateTime? HechoUtc { get; set; }
    public bool Comunicable { get; set; }
}