namespace AulaInnova.API.DTOs.Admin.Matricula;

public class MatriculaResponse
{
    public long Id { get; set; }
    public long EstudianteId { get; set; }
    public long GrupoId { get; set; }
    public DateOnly Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public bool Anulada { get; set; }
}