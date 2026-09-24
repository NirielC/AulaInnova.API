using AulaInnova.API.DTOs.Incidents;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IncidentController : ControllerBase
{
    private readonly IncidentService _incidentService;

    public IncidentController(IncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    [HttpPost]
    public async Task<ActionResult<IncidentResponse>> Create(
        CreateIncidentRequest request)
    {
        try
        {
            var response = await _incidentService.CreateAsync(
                request,
                User);

            if (response is null)
            {
                return BadRequest(new
                {
                    mensaje = "No se pudo registrar la incidencia."
                });
            }

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("observation")]
    public async Task<IActionResult> AddObservation(
        AddIncidentObservationRequest request)
    {
        try
        {
            var success = await _incidentService.AddObservationAsync(
                request,
                User);

            if (!success)
            {
                return BadRequest(new
                {
                    mensaje = "No se pudo registrar la observación."
                });
            }

            return Ok(new
            {
                mensaje = "Observación registrada correctamente."
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}