namespace AulaInnova.API.DTOs.Estacion;

/// <summary>
/// Lo que la estación necesita saber de sí misma al arrancar.
/// </summary>
public class ConfiguracionEstacionResponse
{
    public long EstacionId { get; set; }
    public string CodigoLogico { get; set; } = null!;
    public string TipoPunto { get; set; } = null!;
    public long? AulaId { get; set; }
    public string? AulaCodigo { get; set; }
    public string EtiquetaPunto { get; set; } = null!;
    public short ExpiracionSelectorS { get; set; }

    // Modos que el servidor tiene habilitados: ASISTENCIA y/o MOVIMIENTOS.
    public List<string> Modos { get; set; } = new();

    public List<MotivoEstacion> Motivos { get; set; } = new();

    public DateTime ServidorUtc { get; set; }
}

public class MotivoEstacion
{
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
}
