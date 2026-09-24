using System.Security.Cryptography;
using System.Text;
using AulaInnova.API.Services;

namespace AulaInnova.API.Middleware;

public class RequestContextMiddleware
{
    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext httpContext,
        RequestContext requestContext)
    {
        var solicitudId = Guid.NewGuid();

        var solicitudTexto =
            $"{httpContext.Request.Method}|{httpContext.Request.Path}|{solicitudId}";

        requestContext.SolicitudId = solicitudId;

        requestContext.HashSolicitud =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(solicitudTexto));

        await _next(httpContext);
    }
}