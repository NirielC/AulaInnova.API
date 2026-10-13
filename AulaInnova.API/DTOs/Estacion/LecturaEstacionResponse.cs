namespace AulaInnova.API.DTOs.Estacion;

public class LecturaEstacionResponse
{
    // ASISTENCIA_REGISTRADA, ASISTENCIA_YA_REGISTRADA,
    // LLEGADA_PERSONAL, SALIDA_PERSONAL, PERSONAL_YA_REGISTRADO,
    // ELEGIR_MOTIVO (estudiante dentro del aula) o
    // CONFIRMAR_REGRESO (estudiante con una salida abierta).
    public string Resultado { get; set; } = null!;

    // Personal: LLEGADA_PERSONAL o SALIDA_PERSONAL (también cuando
    // Resultado es PERSONAL_YA_REGISTRADO, para saber cuál fue).
    public string? Accion { get; set; }

    // ESTUDIANTE, PROFESOR, AUXILIAR, ADMINISTRADOR o PERSONAL.
    public string TipoPersona { get; set; } = null!;

    // Para la pantalla de 16 caracteres, sin tildes.
    public string NombreCorto { get; set; } = null!;

    // Asistencia: PUNTUAL, TARDIA o PENDIENTE.
    public string? Puntualidad { get; set; }

    // Personal: clase que tiene en esta aula ahora (si hay).
    public long? AsignacionId { get; set; }
    public string? Clase { get; set; }

    // Movimientos: motivo y hora de la salida abierta.
    public string? MotivoSalidaAbierta { get; set; }
    public DateTime? SalidaAbiertaUtc { get; set; }

    // false = se guardó como POR_REVISAR (personal sin clase en esta aula).
    public bool Aceptado { get; set; } = true;
}
