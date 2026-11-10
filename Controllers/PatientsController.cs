using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.Exceptions;
using HospitalManagement.Api.Helpers;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/patients")]
public class PatientsController(IPatientService patientService) : ControllerBase
{
    [HttpGet]
    [HasPermission("patients.read")]
    [ProducesResponseType(typeof(ApiResponse<PatientPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiResponse.Failure("Page must be positive and pageSize must be between 1 and 100."));

        var result = await patientService.GetPageAsync(search, page, pageSize, cancellationToken);
        return Ok(ApiResponse<PatientPageDto>.Ok(result));
    }

    /// <summary>
    /// The signed-in patient's own record. The patient is resolved from the authenticated account on the server;
    /// no patient id is accepted from the client, so one patient can never address another patient's data here.
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(typeof(ApiResponse<PatientListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var patient = await patientService.GetOrCreateForUserAsync(User.GetRequiredUserId(), cancellationToken);
        if (patient is null)
            throw new NotFoundException("No patient record is linked to this account.");

        return Ok(ApiResponse<PatientListItemDto>.Ok(patient));
    }

    /// <summary>Lets the signed-in patient complete or correct their own profile (date of birth, address, emergency contact, ...).</summary>
    [HttpPut("me")]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(typeof(ApiResponse<PatientListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMine(UpdatePatientProfileRequest request, CancellationToken cancellationToken)
    {
        var patient = await patientService.UpdateMineAsync(User.GetRequiredUserId(), request, cancellationToken);
        return Ok(ApiResponse<PatientListItemDto>.Ok(patient, "Profile updated."));
    }

    [HttpPut("{id:guid}")]
    [HasPermission("patients.manage")]
    [ProducesResponseType(typeof(ApiResponse<PatientListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, UpdatePatientProfileRequest request, CancellationToken cancellationToken)
    {
        var patient = await patientService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<PatientListItemDto>.Ok(patient, "Patient updated."));
    }

    [HttpGet("{id:guid}")]
    [HasPermission("patients.read")]
    [ProducesResponseType(typeof(ApiResponse<PatientListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var patient = await patientService.GetByIdAsync(id, cancellationToken);
        if (patient is null)
            return NotFound(ApiResponse.Failure("Patient was not found."));

        return Ok(ApiResponse<PatientListItemDto>.Ok(patient));
    }

    [HttpPost]
    [HasPermission("patients.create")]
    [ProducesResponseType(typeof(ApiResponse<PatientListItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        CreatePatientRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(ApiResponse.Failure("Date of birth cannot be in the future."));

        var patient = await patientService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = patient.Id }, ApiResponse<PatientListItemDto>.Ok(patient, "Patient registered."));
    }
}