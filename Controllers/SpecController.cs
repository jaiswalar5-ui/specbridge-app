using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpecBridge.Models;
using SpecBridge.Services;

namespace SpecBridge.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("GenerateSpecPolicy")]
public class SpecController : ControllerBase
{
    private readonly ILogger<SpecController> _logger;
    private readonly IAiService _aiService;
    private readonly IPdfExportService _pdfExportService;

    public SpecController(
        ILogger<SpecController> logger,
        IAiService aiService,
        IPdfExportService pdfExportService)
    {
        _logger = logger;
        _aiService = aiService;
        _pdfExportService = pdfExportService;
    }

    [HttpPost("/api/generate-spec")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult> GenerateSpec(
        [FromBody] SpecRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest(new { error = "Prompt is required." });
        }

        if (request.Prompt.Length > 20_000)
        {
            return BadRequest(new { error = "Prompt must be 20,000 characters or fewer." });
        }

        _logger.LogInformation("Processing spec generation request");
        var specification = await _aiService.GenerateSpecAsync(request.Prompt, cancellationToken);

        var errorTitles = new[] { "Configuration Error", "API Error", "Parsing Error", "Validation Error" };
        if (specification is null || errorTitles.Contains(specification.Title))
        {
            return StatusCode(500, new { success = false, error = specification?.Summary ?? "An unknown error occurred." });
        }

        return Ok(new
        {
            Status = "Success",
            Message = "Specification generated successfully.",
            Timestamp = DateTime.UtcNow,
            Specification = specification
        });
    }

    [HttpPost("/api/export-pdf")]
    [Produces("application/pdf")]
    public ActionResult ExportPdf([FromBody] SpecResponse? specification)
    {
        if (specification is null)
        {
            return BadRequest(new { error = "A specification document is required." });
        }

        var pdfStream = _pdfExportService.CreatePdf(specification);
        return File(pdfStream, "application/pdf", "specbridge-specification.pdf");
    }
}
