namespace AulaInnova.API.Models
{
    public class ZonaVisual
    {
        public long Id { get; set; }
        public long AulaId { get; set; }
        public string Codigo { get; set; } = null!;
        public int Revision { get; set; }
        public string GeometriaJson { get; set; } = null!;
        public bool Habilitada { get; set; }

        public Aula Aula { get; set; } = null!;
    }
}
