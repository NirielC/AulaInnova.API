namespace AulaInnova.API.DTOs.Admin.Asignacion;

public class CreateAsignacionRequest
{
    public long JornadaId { get; set; }
    public long GrupoId { get; set; }
    public long? HorarioClaseId { get; set; }
    public long PersonalId { get; set; }
    public long AulaId { get; set; }
    public string Tipo { get; set; } = null!;
    public DateTime InicioUtc { get; set; }
    public DateTime FinUtc { get; set; }
    public string? Actividad { get; set; }
}