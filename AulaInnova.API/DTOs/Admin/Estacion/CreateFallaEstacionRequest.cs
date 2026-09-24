namespace AulaInnova.API.DTOs.Admin.Estacion;

public class CreateFallaEstacionRequest
{
    public long EstacionId { get; set; }
    public string Descripcion { get; set; } = null!;
}