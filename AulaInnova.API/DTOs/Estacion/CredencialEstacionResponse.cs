namespace AulaInnova.API.DTOs.Estacion;

/// <summary>
/// Quién es el dueño de una tarjeta, sin registrar nada. La estación lo
/// usa para el menú de administración (solo ADMINISTRADOR) y para
/// recordar, sin red, qué tarjetas son de personal.
/// </summary>
public class CredencialEstacionResponse
{
    public string TipoPersona { get; set; } = null!;
    public string NombreCorto { get; set; } = null!;
    public bool EsAdministrador { get; set; }
}
