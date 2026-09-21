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
    private readonly ISpecGeneratorService _specGeneratorService;
    private readonly IPdfExportService _pdfExportService;

    public SpecController(
        ILogger<SpecController> logger,
        ISpecGeneratorService specGeneratorService,
        IPdfExportService pdfExportService)
    {
        _logger = logger;
        _specGeneratorService = specGeneratorService;
        _pdfExportService = pdfExportService;
    }

    [HttpPost("/api/generate-spec")]
    [ProducesResponseType(typeof(SpecResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SpecResponse>> GenerateSpec(
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

        try
        {
            _logger.LogInformation("Processing spec generation request");
            var specification = await _specGeneratorService.GenerateSpecAsync(request.Prompt, cancellationToken);

            return Ok(new SpecResponse
            {
                Status = "Success",
                Message = "Specification generated successfully.",
                Timestamp = DateTime.UtcNow,
                Specification = specification
            });
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception, "Specification generation configuration or parsing failed");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = exception.Message });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Specification provider request failed");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "The specification provider is unavailable." });
        }
    }

    [HttpPost("/api/export-pdf")]
    [Produces("application/pdf")]
    public ActionResult ExportPdf([FromBody] SpecificationDocument? specification)
    {
        if (specification is null)
        {
            return BadRequest(new { error = "A specification document is required." });
        }

        var pdf = _pdfExportService.CreatePdf(specification);
        return File(pdf, "application/pdf", "specbridge-specification.pdf");
    }
}
