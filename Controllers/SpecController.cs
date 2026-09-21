using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpecBridge.Models;

namespace SpecBridge.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("GenerateSpecPolicy")]
public class SpecController : ControllerBase
{
    private readonly ILogger<SpecController> _logger;

    public SpecController(ILogger<SpecController> logger)
    {
        _logger = logger;
    }

    [HttpPost("/api/generate-spec")]
    [ProducesResponseType(typeof(SpecResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult GenerateSpec([FromBody] SpecRequest? request)
    {
        _logger.LogInformation("Processing spec generation request");

        return Ok(new SpecResponse
        {
            Status = "Success",
            Message = "Spec generation endpoint reached successfully.",
            Timestamp = DateTime.UtcNow
        });
    }
}
