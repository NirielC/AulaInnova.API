namespace AulaInnova.API.DTOs.Coverage;

public class CreateCoverageNeedRequest
{
    public long AsignacionOrigenId { get; set; }
    public string Motivo { get; set; } = null!;
}