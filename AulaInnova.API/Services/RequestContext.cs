namespace AulaInnova.API.Services;

public class RequestContext
{
    public Guid SolicitudId { get; set; }

    public byte[] HashSolicitud { get; set; } = null!;
}