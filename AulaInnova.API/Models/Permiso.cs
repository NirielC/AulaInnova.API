namespace AulaInnova.API.Models;

public class Permiso
{
    public string Codigo { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public bool Habilitado { get; set; }
}
