using HospitalManagement.Api.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

/// <summary>
/// Development-only endpoints used to verify the global exception handling.
/// Returns 404 outside the Development environment.
/// </summary>
[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController(IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("not-found")]
    public IActionResult ThrowNotFound()
    {
        if (!environment.IsDevelopment()) return NotFound();
        throw new NotFoundException("Patient", 42);
    }

    [HttpGet("unhandled")]
    public IActionResult ThrowUnhandled()
    {
        if (!environment.IsDevelopment()) return NotFound();
        throw new InvalidOperationException("Simulated internal failure with sensitive details.");
    }
}
