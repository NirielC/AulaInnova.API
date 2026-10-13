namespace AulaInnova.API.Services.Exceptions;

/// <summary>
/// Rechazo de una lectura de estación por una regla de negocio
/// (credencial no registrada, sin jornada, salida ya abierta...).
///
/// El controlador lo convierte en 400 { codigo, mensaje }. La estación
/// usa <see cref="Codigo"/> para mostrar el mensaje correcto en su
/// pantalla y para saber que NO debe reintentar (un 400 no se arregla
/// reenviando lo mismo).
/// </summary>
public class RechazoEstacionException : Exception
{
    public string Codigo { get; }

    public RechazoEstacionException(string codigo, string mensaje)
        : base(mensaje)
    {
        Codigo = codigo;
    }
}
