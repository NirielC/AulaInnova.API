namespace AulaInnova.API.Models;

public class Estudiante
{
    public long PersonaId { get; set; }
    public string TipoPersona { get; set; } = "ESTUDIANTE";
    public string Codigo { get; set; } = null!;

    public Persona Persona { get; set; } = null!;
}
