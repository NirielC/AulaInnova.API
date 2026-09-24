using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Movements;
using AulaInnova.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace AulaInnova.API.Services;

public class MovementService
{
    private readonly AulaInnovaDbContext _context;

    public MovementService(AulaInnovaDbContext context)
    {
        _context = context;
    }

    public async Task<MovementResponse?> RegisterExitAsync(
        MovementExitRequest request)
    {
        var utcNow = request.OcurridoUtc ?? DateTime.UtcNow;

        // 1. Validar estación habilitada
        var estacion = await _context.Estaciones
            .FirstOrDefaultAsync(e =>
                e.Id == request.EstacionId &&
                e.Habilitada);

        if (estacion is null)
            return null;

        // 2. Buscar configuración vigente
        var configuracion = await _context.EstacionesConfiguracion
            .FirstOrDefaultAsync(c =>
                c.EstacionId == estacion.Id &&
                c.VigenteDesdeUtc <= utcNow &&
                (c.VigenteHastaUtc == null ||
                 c.VigenteHastaUtc >= utcNow));

        if (configuracion is null)
            return null;

        // 3. Confirmar operación SALIDA_AULA
        var operacion = await _context.EstacionesOperaciones
            .FirstOrDefaultAsync(o =>
                o.EstacionConfiguracionId == configuracion.Id &&
                o.Accion == "SALIDA_AULA" &&
                o.ModoInterfaz == "MOVIMIENTOS");

        if (operacion is null)
            return null;

        // 4. La salida debe estar asociada a un aula
        if (configuracion.AulaId is null)
            return null;

        // 5. Validar motivo de salida
        var motivo = await _context.MotivosSalida
            .FirstOrDefaultAsync(m =>
                m.Codigo == request.MotivoCodigo &&
                m.Habilitado);

        if (motivo is null)
            return null;

        // 6. Buscar credencial NFC válida
        var credencial = await _context.CredencialesNfc
            .Include(c => c.Persona)
            .FirstOrDefaultAsync(c =>
                c.Referencia == request.ReferenciaNfc &&
                c.Activa &&
                c.DesdeUtc <= utcNow &&
                (c.HastaUtc == null || c.HastaUtc >= utcNow));

        if (credencial is null)
            return null;

        // 7. Validar persona
        var persona = credencial.Persona;

        if (persona is null || !persona.Habilitada)
            return null;

        // 8. Confirmar estudiante
        var estudiante = await _context.Estudiantes
            .FirstOrDefaultAsync(e =>
                e.PersonaId == persona.Id);

        if (estudiante is null)
            return null;

        // 9. Buscar matrícula vigente
        var fechaActual = DateOnly.FromDateTime(utcNow);

        var matricula = await _context.Matriculas
            .FirstOrDefaultAsync(m =>
                m.EstudianteId == estudiante.PersonaId &&
                !m.Anulada &&
                m.Desde <= fechaActual &&
                (m.Hasta == null || m.Hasta >= fechaActual));

        if (matricula is null)
            return null;

        // 10. Buscar jornada actual
        var jornada = await _context.Jornadas
            .FirstOrDefaultAsync(j =>
                j.InicioUtc <= utcNow &&
                j.FinUtc >= utcNow &&
                j.Estado == "ABIERTA");

        if (jornada is null)
            return null;

        // 11. Verificar que no exista otra salida abierta
        var salidaAbierta = await _context.SalidasTemporales
            .FirstOrDefaultAsync(s =>
                s.EstudianteId == estudiante.PersonaId &&
                s.Estado == "ABIERTA");

        if (salidaAbierta is not null)
            return null;

        // 12. Calidad de hora
        var calidadHora = request.OcurridoUtc.HasValue
            ? "DECLARADA"
            : "FIABLE";

        // 13. Calcular expiración
        DateTime? expiraUtc = null;

        if (configuracion.ExpiracionSelectorS > 0)
        {
            expiraUtc = utcNow.AddSeconds(
                configuracion.ExpiracionSelectorS);
        }

        // 14. Crear evento de salida
        var eventoId = Guid.NewGuid();

        var contenidoHash =
            $"{request.ReferenciaNfc}|{request.EstacionId}|SALIDA_AULA|{utcNow:O}";

        var hashSolicitud = SHA256.HashData(
            Encoding.UTF8.GetBytes(contenidoHash));

        var evento = new Evento
        {
            Id = eventoId,
            HashSolicitud = hashSolicitud,
            Origen = "ESTACION",
            Accion = "SALIDA_AULA",
            PersonaId = persona.Id,
            CredencialId = credencial.Id,
            EstacionId = estacion.Id,
            EstacionConfiguracionId = configuracion.Id,
            AulaId = configuracion.AulaId,
            JornadaId = jornada.Id,
            MatriculaId = matricula.Id,
            MotivoCodigo = motivo.Codigo,
            OcurridoUtc = utcNow,
            RecibidoUtc = DateTime.UtcNow,
            CalidadHora = calidadHora,
            Estado = "ACEPTADO",
            CodigoResultado = "SALIDA_REGISTRADA"
        };

        _context.Eventos.Add(evento);

        // 15. Crear salida temporal
        var salida = new SalidaTemporal
        {
            EstudianteId = estudiante.PersonaId,
            JornadaId = jornada.Id,
            MatriculaId = matricula.Id,
            AulaId = configuracion.AulaId.Value,
            MotivoCodigo = motivo.Codigo,
            EventoSalidaId = eventoId,
            SalidaUtc = utcNow,
            ExpiraUtc = expiraUtc,
            HorasFiables = calidadHora == "FIABLE",
            Estado = "ABIERTA",
            CreadoUtc = DateTime.UtcNow
        };

        _context.SalidasTemporales.Add(salida);

        await _context.SaveChangesAsync();

        return new MovementResponse
        {
            SalidaTemporalId = salida.Id,
            Accion = "SALIDA_AULA",
            Estado = salida.Estado,
            SalidaUtc = salida.SalidaUtc,
            RegresoUtc = salida.RegresoUtc,
            ExpiraUtc = salida.ExpiraUtc
        };
    }

    public async Task<MovementResponse?> RegisterReturnAsync(
        MovementReturnRequest request)
    {
        var utcNow = request.OcurridoUtc ?? DateTime.UtcNow;

        // 1. Validar estación habilitada
        var estacion = await _context.Estaciones
            .FirstOrDefaultAsync(e =>
                e.Id == request.EstacionId &&
                e.Habilitada);

        if (estacion is null)
            return null;

        // 2. Buscar configuración vigente
        var configuracion = await _context.EstacionesConfiguracion
            .FirstOrDefaultAsync(c =>
                c.EstacionId == estacion.Id &&
                c.VigenteDesdeUtc <= utcNow &&
                (c.VigenteHastaUtc == null ||
                 c.VigenteHastaUtc >= utcNow));

        if (configuracion is null)
            return null;

        // 3. Confirmar operación REGRESO_AULA
        var operacion = await _context.EstacionesOperaciones
            .FirstOrDefaultAsync(o =>
                o.EstacionConfiguracionId == configuracion.Id &&
                o.Accion == "REGRESO_AULA" &&
                o.ModoInterfaz == "MOVIMIENTOS");

        if (operacion is null)
            return null;

        // 4. Buscar credencial NFC válida
        var credencial = await _context.CredencialesNfc
            .Include(c => c.Persona)
            .FirstOrDefaultAsync(c =>
                c.Referencia == request.ReferenciaNfc &&
                c.Activa &&
                c.DesdeUtc <= utcNow &&
                (c.HastaUtc == null || c.HastaUtc >= utcNow));

        if (credencial is null)
            return null;

        // 5. Validar persona
        var persona = credencial.Persona;

        if (persona is null || !persona.Habilitada)
            return null;

        // 6. Confirmar estudiante
        var estudiante = await _context.Estudiantes
            .FirstOrDefaultAsync(e =>
                e.PersonaId == persona.Id);

        if (estudiante is null)
            return null;

        // 7. Buscar salida abierta
        var salida = await _context.SalidasTemporales
            .FirstOrDefaultAsync(s =>
                s.EstudianteId == estudiante.PersonaId &&
                s.Estado == "ABIERTA");

        if (salida is null)
            return null;

        // 8. Validar que el regreso no sea anterior a la salida
        if (salida.SalidaUtc.HasValue &&
            utcNow < salida.SalidaUtc.Value)
        {
            return null;
        }

        // 9. Calidad de hora del regreso
        var calidadHora = request.OcurridoUtc.HasValue
            ? "DECLARADA"
            : "FIABLE";

        // 10. Crear evento de regreso
        var eventoId = Guid.NewGuid();

        var contenidoHash =
            $"{request.ReferenciaNfc}|{request.EstacionId}|REGRESO_AULA|{utcNow:O}";

        var hashSolicitud = SHA256.HashData(
            Encoding.UTF8.GetBytes(contenidoHash));

        var evento = new Evento
        {
            Id = eventoId,
            HashSolicitud = hashSolicitud,
            Origen = "ESTACION",
            Accion = "REGRESO_AULA",
            PersonaId = persona.Id,
            CredencialId = credencial.Id,
            EstacionId = estacion.Id,
            EstacionConfiguracionId = configuracion.Id,
            AulaId = salida.AulaId,
            JornadaId = salida.JornadaId,
            MatriculaId = salida.MatriculaId,
            OcurridoUtc = utcNow,
            RecibidoUtc = DateTime.UtcNow,
            CalidadHora = calidadHora,
            Estado = "ACEPTADO",
            CodigoResultado = "REGRESO_REGISTRADO"
        };

        _context.Eventos.Add(evento);

        // 11. Cerrar salida temporal
        salida.EventoRegresoId = eventoId;
        salida.RegresoUtc = utcNow;
        salida.Estado = "CERRADA";

        salida.HorasFiables =
            salida.HorasFiables &&
            calidadHora == "FIABLE";

        // 12. Guardar
        await _context.SaveChangesAsync();

        return new MovementResponse
        {
            SalidaTemporalId = salida.Id,
            Accion = "REGRESO_AULA",
            Estado = salida.Estado,
            SalidaUtc = salida.SalidaUtc,
            RegresoUtc = salida.RegresoUtc,
            ExpiraUtc = salida.ExpiraUtc
        };
    }
}
