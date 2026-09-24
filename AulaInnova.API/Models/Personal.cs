namespace AulaInnova.API.Models;

public class Personal
{
    public long PersonaId { get; set; }
    public string TipoPersona { get; set; } = "PERSONAL";
    public string Codigo { get; set; } = null!;

    public Persona Persona { get; set; } = null!;
}
