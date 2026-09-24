namespace AulaInnova.API.DTOs.Auth;

public class LoginResponse
{
    public string Token { get; set; } = null!;
    public DateTime ExpiraUtc { get; set; }
}
