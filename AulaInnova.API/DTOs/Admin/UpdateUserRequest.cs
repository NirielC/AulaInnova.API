namespace AulaInnova.API.DTOs.Admin;

public class UpdateUserRequest
{
    public string Email { get; set; } = null!;
    public long PersonaId { get; set; }
    public string Rol { get; set; } = null!;
}