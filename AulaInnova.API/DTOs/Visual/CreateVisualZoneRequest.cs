namespace AulaInnova.API.DTOs.Visual;

public class CreateVisualZoneRequest
{
    public long AulaId { get; set; }
    public string Codigo { get; set; } = null!;
    public int Revision { get; set; }
    public string GeometriaJson { get; set; } = null!;
}