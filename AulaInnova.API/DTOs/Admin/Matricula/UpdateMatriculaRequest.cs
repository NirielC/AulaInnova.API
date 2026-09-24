namespace AulaInnova.API.DTOs.Admin.Matricula;

public class UpdateMatriculaRequest
{
    public long GrupoId { get; set; }
    public DateOnly Desde { get; set; }
    public DateOnly? Hasta { get; set; }
}