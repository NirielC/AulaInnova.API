namespace AulaInnova.API.DTOs.Admin;

public class CreateStaffRequest
{
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public string Codigo { get; set; } = null!;
}