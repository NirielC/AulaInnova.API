namespace AulaInnova.API.Models;

public class Asignatura
{
    public long Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public bool Habilitada { get; set; }
    public byte[] Version { get; set; } = null!;
}
