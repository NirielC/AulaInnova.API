namespace AulaInnova.API.DTOs.Admin;

public class PersonResponse
{
    public long Id { get; set; }
    public string TipoPersona { get; set; } = null!;
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public string Codigo { get; set; } = null!;
    public bool Habilitada { get; set; }
    public Guid? UsuarioId { get; set; }
}