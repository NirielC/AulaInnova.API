namespace AulaInnova.API.Models
{
    public class ContextoClase
    {
        public long Id { get; set; }
        public long AsignacionId { get; set; }
        public Guid AutorId { get; set; }
        public DateTime InicioUtc { get; set; }
        public DateTime FinUtc { get; set; }
        public string Descripcion { get; set; } = null!;
        public DateTime CreadoUtc { get; set; }
        public byte[] Version { get; set; } = null!;

        public Asignacion Asignacion { get; set; } = null!;
        public ApplicationUser Autor { get; set; } = null!;
    }
}
