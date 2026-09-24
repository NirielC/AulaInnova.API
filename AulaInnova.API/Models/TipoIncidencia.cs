namespace AulaInnova.API.Models;

public class TipoIncidencia
{
    public long Id { get; set; }

    public long CategoriaId { get; set; }

    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public bool Habilitado { get; set; }

    public byte[] Version { get; set; } = null!;

    public CategoriaIncidencia Categoria { get; set; } = null!;
    public ICollection<Incidencia> Incidencias { get; set; } = new List<Incidencia>();
}
