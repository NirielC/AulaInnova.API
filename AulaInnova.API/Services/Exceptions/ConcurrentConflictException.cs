namespace AulaInnova.API.Services.Exceptions;

/// <summary>
/// Se lanza cuando SaveChangesAsync falla por violar un índice único
/// filtrado (por ejemplo UX_sesion_visual_aula_activa o
/// UX_zona_visual_habilitada) porque otra solicitud ganó la carrera.
/// </summary>
public class ConcurrentConflictException : Exception
{
    public ConcurrentConflictException(string message) : base(message)
    {
    }
}
