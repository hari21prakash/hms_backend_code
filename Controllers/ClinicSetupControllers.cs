using HospitalManagement.Api.Authorization;
using HospitalManagement.Api.DTOs;
using HospitalManagement.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/departments")]
public class DepartmentsController(IClinicSetupService setup) : ControllerBase
{
    [HttpGet]
    [HasPermission("departments.read")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<DepartmentDto>>.Ok(await setup.GetDepartmentsAsync(cancellationToken)));

    [HttpPost]
    [HasPermission("departments.manage")]
    public async Task<IActionResult> Create(SaveDepartmentRequest request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created,
            ApiResponse<DepartmentDto>.Ok(await setup.CreateDepartmentAsync(request, cancellationToken), "Department created."));

    [HttpPut("{id:guid}")]
    [HasPermission("departments.manage")]
    public async Task<IActionResult> Update(Guid id, SaveDepartmentRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<DepartmentDto>.Ok(await setup.UpdateDepartmentAsync(id, request, cancellationToken), "Department updated."));
}

[ApiController]
[Authorize]
[Route("api/staff")]
public class StaffController(IClinicSetupService setup) : ControllerBase
{
    [HttpGet("doctors")]
    [HasPermission("staff.read")]
    public async Task<IActionResult> GetDoctors(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<DoctorStaffDto>>.Ok(await setup.GetDoctorsAsync(cancellationToken)));

    [HttpPut("doctors/{id:guid}")]
    [HasPermission("staff.manage")]
    public async Task<IActionResult> UpdateDoctor(Guid id, UpdateDoctorRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<DoctorStaffDto>.Ok(await setup.UpdateDoctorAsync(id, request, cancellationToken), "Doctor profile updated."));

    [HttpGet("nurses")]
    [HasPermission("staff.read")]
    public async Task<IActionResult> GetNurses(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<NurseStaffDto>>.Ok(await setup.GetNursesAsync(cancellationToken)));

    [HttpPut("nurses/{id:guid}")]
    [HasPermission("staff.manage")]
    public async Task<IActionResult> UpdateNurse(Guid id, UpdateNurseRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<NurseStaffDto>.Ok(await setup.UpdateNurseAsync(id, request, cancellationToken), "Nurse profile updated."));
}

[ApiController]
[Authorize]
[Route("api/lab-tests")]
public class LabTestsController(IClinicSetupService setup) : ControllerBase
{
    [HttpGet]
    [HasPermission("laboratory.manage")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<LabTestDto>>.Ok(await setup.GetLabTestsAsync(cancellationToken)));

    [HttpPost]
    [HasPermission("laboratory.manage")]
    public async Task<IActionResult> Create(SaveLabTestRequest request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created,
            ApiResponse<LabTestDto>.Ok(await setup.CreateLabTestAsync(request, cancellationToken), "Lab test created."));

    [HttpPut("{id:guid}")]
    [HasPermission("laboratory.manage")]
    public async Task<IActionResult> Update(Guid id, SaveLabTestRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<LabTestDto>.Ok(await setup.UpdateLabTestAsync(id, request, cancellationToken), "Lab test updated."));
}
