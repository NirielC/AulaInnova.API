using AulaInnova.API.DTOs.Coverage;
using AulaInnova.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AulaInnova.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CoverageController : ControllerBase
{
    private readonly CoverageService _coverageService;

    public CoverageController(CoverageService coverageService)
    {
        _coverageService = coverageService;
    }

    [HttpPost("need")]
    public async Task<ActionResult<CoverageResponse>> CreateNeed(
        CreateCoverageNeedRequest request)
    {
        try
        {
            var response =
                await _coverageService.CreateNeedAsync(
                    request,
                    User);

            if (response is null)
            {
                return BadRequest(new
                {
                    mensaje =
                        "No se pudo crear la necesidad de cobertura."
                });
            }

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("assign")]
    public async Task<ActionResult<CoverageResponse>> Assign(
        AssignCoverageRequest request)
    {
        try
        {
            var response =
                await _coverageService.AssignAsync(
                    request,
                    User);

            if (response is null)
            {
                return BadRequest(new
                {
                    mensaje =
                        "No se pudo asignar la cobertura."
                });
            }

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("verify")]
    public async Task<ActionResult<CoverageResponse>> Verify(
        VerifyCoverageRequest request)
    {
        try
        {
            var response =
                await _coverageService.VerifyAsync(
                    request,
                    User);

            if (response is null)
            {
                return BadRequest(new
                {
                    mensaje =
                        "No se pudo verificar la cobertura."
                });
            }

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}