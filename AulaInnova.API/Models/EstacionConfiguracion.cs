namespace AulaInnova.API.Models;

public class EstacionConfiguracion
{
    public long Id { get; set; }
    public long EstacionId { get; set; }
    public string TipoPunto { get; set; } = null!;
    public long? AulaId { get; set; }
    public string EtiquetaPunto { get; set; } = null!;
    public short ExpiracionSelectorS { get; set; }
    public DateTime VigenteDesdeUtc { get; set; }
    public DateTime? VigenteHastaUtc { get; set; }
    public Guid ConfiguradaPor { get; set; }
    public string MotivoCambio { get; set; } = null!;
    public DateTime CreadoUtc { get; set; }
    public byte[] Version { get; set; } = null!;

    public Estacion Estacion { get; set; } = null!;
    public Aula? Aula { get; set; }
    public ApplicationUser ConfiguradaPorUsuario { get; set; } = null!;

    public ICollection<EstacionOperacion> Operaciones { get; set; }
        = new List<EstacionOperacion>();
}
