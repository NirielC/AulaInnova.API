namespace AulaInnova.API.DTOs.Admin.Estacion;

public class CreateEstacionRequest
{
    public string CodigoLogico { get; set; } = null!;
    public string? MacInventario { get; set; }
}