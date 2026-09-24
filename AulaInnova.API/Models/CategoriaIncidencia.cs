namespace AulaInnova.API.Models;

public class CategoriaIncidencia
{
    public long Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public bool Habilitada { get; set; }
    public byte[] Version { get; set; } = null!;

    public ICollection<TipoIncidencia> TiposIncidencia { get; set; } = new List<TipoIncidencia>();
}
