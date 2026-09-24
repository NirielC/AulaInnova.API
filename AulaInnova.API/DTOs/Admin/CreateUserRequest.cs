namespace AulaInnova.API.DTOs.Admin;

public class CreateUserRequest
{
    public string Usuario { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public long PersonaId { get; set; }
    public string Rol { get; set; } = null!;
}