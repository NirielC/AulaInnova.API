namespace AulaInnova.API.DTOs.Admin.Nfc;

public class CreateNfcCredentialRequest
{
    public long PersonaId { get; set; }
    public string Referencia { get; set; } = null!;
    public DateTime DesdeUtc { get; set; }
}