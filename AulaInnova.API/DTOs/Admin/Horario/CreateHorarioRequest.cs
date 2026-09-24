namespace AulaInnova.API.DTOs.Admin.Horario;

public class CreateHorarioRequest
{
    public long AsignaturaId { get; set; }
    public long GrupoId { get; set; }
    public long DocenteId { get; set; }
    public long AulaId { get; set; }
    public byte DiaSemana { get; set; }
    public DateOnly VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }
}