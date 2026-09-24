namespace AulaInnova.API.DTOs.Auth;

public class LoginRequest
{
    public string Usuario { get; set; } = null!;
    public string Password { get; set; } = null!;
}
