namespace AulaInnova.API.DTOs.Admin;

public class UpdateGroupRequest
{
    public short Ciclo { get; set; }
    public string Grado { get; set; } = null!;
    public string Seccion { get; set; } = null!;
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
}