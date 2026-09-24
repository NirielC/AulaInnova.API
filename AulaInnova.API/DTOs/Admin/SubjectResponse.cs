namespace AulaInnova.API.DTOs.Admin;

public class SubjectResponse
{
    public long Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public bool Habilitada { get; set; }
}