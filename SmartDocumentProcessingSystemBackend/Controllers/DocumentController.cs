using Microsoft.AspNetCore.Mvc;
using SmartDocumentProcessingSystem.Dtos;
using SmartDocumentProcessingSystem.Services;

namespace SmartDocumentProcessingSystem.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _documentService.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DocumentDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var document = await _documentService.GetByIdAsync(id, cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        return Ok(await _documentService.GetDashboardSummaryAsync(cancellationToken));
    }

    [HttpPost("upload")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { Error = "A non-empty file is required." });
        }

        var document = await _documentService.ProcessUploadAsync(file, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = document.Id }, document);
    }

    [HttpPost("import-samples")]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> ImportSamples([FromQuery] bool refreshExisting, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _documentService.ImportSamplesAsync(refreshExisting, cancellationToken));
        }
        catch (DirectoryNotFoundException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<DocumentDto>> Update(int id, [FromBody] UpdateDocumentRequest request, CancellationToken cancellationToken)
    {
        var document = await _documentService.UpdateAsync(id, request, cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpPost("{id:int}/validate")]
    public async Task<ActionResult<DocumentDto>> Validate(int id, CancellationToken cancellationToken)
    {
        var document = await _documentService.RevalidateAsync(id, cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpPost("{id:int}/confirm")]
    public async Task<ActionResult<DocumentDto>> Confirm(int id, CancellationToken cancellationToken)
    {
        var document = await _documentService.ConfirmAsync(id, cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<DocumentDto>> Reject(int id, CancellationToken cancellationToken)
    {
        var document = await _documentService.RejectAsync(id, cancellationToken);
        return document is null ? NotFound() : Ok(document);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _documentService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
