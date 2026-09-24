namespace AulaInnova.API.DTOs.Admin.Matricula;

public class CreateMatriculaRequest
{
    public long EstudianteId { get; set; }
    public long GrupoId { get; set; }
    public DateOnly Desde { get; set; }
    public DateOnly? Hasta { get; set; }
}