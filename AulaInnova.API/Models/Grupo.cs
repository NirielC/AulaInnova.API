namespace AulaInnova.API.Models;

public class Grupo
{
    public long Id { get; set; }
    public short Ciclo { get; set; }
    public string Grado { get; set; } = null!;
    public string Seccion { get; set; } = null!;
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public bool Habilitado { get; set; }
    public byte[] Version { get; set; } = null!;
}
