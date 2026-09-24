namespace AulaInnova.API.Models;

public class Concesion
{
    public long Id { get; set; }

    public Guid UsuarioId { get; set; }
    public string PermisoCodigo { get; set; } = null!;

    public string Ambito { get; set; } = null!;
    public long? GrupoId { get; set; }

    public DateTime DesdeUtc { get; set; }
    public DateTime? HastaUtc { get; set; }

    public bool Habilitada { get; set; }

    public byte[] Version { get; set; } = null!;

    public ApplicationUser Usuario { get; set; } = null!;
    public Permiso Permiso { get; set; } = null!;
    public Grupo? Grupo { get; set; }
}