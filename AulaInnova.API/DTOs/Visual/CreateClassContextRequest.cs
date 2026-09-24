namespace AulaInnova.API.DTOs.Visual;

public class CreateClassContextRequest
{
    public long AsignacionId { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public string Descripcion { get; set; } = null!;
}