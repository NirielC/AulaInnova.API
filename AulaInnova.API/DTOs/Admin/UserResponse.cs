namespace AulaInnova.API.DTOs.Admin;

public class UserResponse
{
    public Guid Id { get; set; }
    public string Usuario { get; set; } = null!;
    public string Email { get; set; } = null!;
    public bool Activo { get; set; }
    public long? PersonaId { get; set; }
    public string? Rol { get; set; }
}