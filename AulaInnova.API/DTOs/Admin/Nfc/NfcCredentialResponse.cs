namespace AulaInnova.API.DTOs.Admin.Nfc;

public class NfcCredentialResponse
{
    public long Id { get; set; }
    public long PersonaId { get; set; }
    public string Referencia { get; set; } = null!;
    public DateTime DesdeUtc { get; set; }
    public DateTime? HastaUtc { get; set; }
    public bool Activa { get; set; }
    public string? MotivoBaja { get; set; }
}