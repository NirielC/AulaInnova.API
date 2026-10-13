using AulaInnova.API.Data;
using AulaInnova.API.DTOs.Profesor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/profesor/horario")]
[Authorize(Roles = "Profesor")]
public class ProfesorHorarioController : ControllerBase
{
    private readonly AulaInnovaDbContext _db;

    public ProfesorHorarioController(AulaInnovaDbContext db)
    {
        _db = db;
    }

    // Horario semanal vigente del profesor que inició sesión.
    // ?dia=1..7 filtra un solo día (1 = lunes ... 7 = domingo, igual que
    // ai.horario_clase.dia_semana, CK_horario_clase_dia).
    [HttpGet]
    public async Task<ActionResult<List<HorarioProfesorResponse>>> GetMiHorario(
        [FromQuery] byte? dia)
    {
        if (dia.HasValue && (dia.Value < 1 || dia.Value > 7))
            return BadRequest(new
            {
                mensaje = "dia debe estar entre 1 (lunes) y 7 (domingo)."
            });

        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return Unauthorized();
        }

        var personaId = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.PersonaId)
            .FirstOrDefaultAsync();

        if (!personaId.HasValue)
            return NotFound(new
            {
                mensaje = "El usuario no está vinculado a una persona."
            });

        var hoy = DateOnly.FromDateTime(DateTime.Now);

        var query = _db.HorariosClase
            .AsNoTracking()
            .Where(h =>
                h.DocenteId == personaId.Value &&
                !h.Anulado &&
                h.VigenteDesde <= hoy &&
                (h.VigenteHasta == null || h.VigenteHasta >= hoy));

        if (dia.HasValue)
            query = query.Where(h => h.DiaSemana == dia.Value);

        var horario = await query
            .OrderBy(h => h.DiaSemana)
            .ThenBy(h => h.HoraInicio)
            .Select(h => new HorarioProfesorResponse
            {
                Id = h.Id,
                DiaSemana = h.DiaSemana,
                HoraInicio = h.HoraInicio,
                HoraFin = h.HoraFin,
                AsignaturaId = h.AsignaturaId,
                Asignatura = h.Asignatura.Nombre,
                GrupoId = h.GrupoId,
                Grupo = h.Grupo.Grado + " " + h.Grupo.Seccion,
                AulaId = h.AulaId,
                Aula = h.Aula.Nombre
            })
            .ToListAsync();

        return Ok(horario);
    }
}
