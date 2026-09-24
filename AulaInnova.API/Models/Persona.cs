namespace AulaInnova.API.Models;

public class Persona
{
    public long Id { get; set; }
    public string TipoPersona { get; set; } = null!;
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public bool Habilitada { get; set; }
    public DateTime CreadoUtc { get; set; }
    public byte[] Version { get; set; } = null!;

    public ApplicationUser? Usuario { get; set; }
    public Estudiante? Estudiante { get; set; }
    public Personal? Personal { get; set; }
}
