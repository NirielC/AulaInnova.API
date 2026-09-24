namespace AulaInnova.API.Configuration;

public class JwtSettings
{
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public string Secret { get; set; } = null!;
    public int ExpirationMinutes { get; set; }
}
