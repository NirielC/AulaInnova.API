namespace AulaInnova.API.Models;

public class CredencialNfc
{
    public long Id { get; set; }
    public string Referencia { get; set; } = null!;
    public long PersonaId { get; set; }
    public DateTime DesdeUtc { get; set; }
    public DateTime? HastaUtc { get; set; }
    public bool Activa { get; set; }
    public string? MotivoBaja { get; set; }
    public DateTime CreadoUtc { get; set; }
    public byte[] Version { get; set; } = null!;

    public Persona Persona { get; set; } = null!;
}
