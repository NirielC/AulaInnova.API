namespace AulaInnova.API.DTOs.Estacion;

public class SesionVisionRequest
{
    // Versión del programa de visión (sesion_visual.version_proceso).
    public string VersionProceso { get; set; } = null!;
}

/// <summary>
/// La sesión visual de la clase que hay AHORA en el aula de la cámara.
/// Si no hay clase, SesionId es null y la cámara no envía mediciones.
/// </summary>
public class SesionVisionResponse
{
    public long? SesionId { get; set; }

    // Las ventanas de medición deben empezar después de esta hora.
    public DateTime? SesionInicioUtc { get; set; }

    public long? AsignacionId { get; set; }
    public string? Clase { get; set; }
    public DateTime? ClaseInicioUtc { get; set; }
    public DateTime? ClaseFinUtc { get; set; }
    public long AulaId { get; set; }

    // Ids que van en cada medición, por código.
    public Dictionary<string, long> Zonas { get; set; } = new();
    public Dictionary<string, long> Indicadores { get; set; } = new();

    public DateTime ServidorUtc { get; set; }
    public string Mensaje { get; set; } = null!;
}
