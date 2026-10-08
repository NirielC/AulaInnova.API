namespace AulaInnova.API.DTOs.Profesor;

public class HorarioProfesorResponse
{
    public long Id { get; set; }
    public byte DiaSemana { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }

    public long AsignaturaId { get; set; }
    public string Asignatura { get; set; } = null!;

    public long GrupoId { get; set; }
    public string Grupo { get; set; } = null!;

    public long AulaId { get; set; }
    public string Aula { get; set; } = null!;
}
