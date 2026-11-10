using HospitalManagement.Api.DTOs;

namespace HospitalManagement.Api.Interfaces;

/// <summary>Master data an administrator must be able to maintain: departments, staff profiles, lab test catalogue.</summary>
public interface IClinicSetupService
{
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken cancellationToken);
    Task<DepartmentDto> CreateDepartmentAsync(SaveDepartmentRequest request, CancellationToken cancellationToken);
    Task<DepartmentDto> UpdateDepartmentAsync(Guid id, SaveDepartmentRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<DoctorStaffDto>> GetDoctorsAsync(CancellationToken cancellationToken);
    Task<DoctorStaffDto> UpdateDoctorAsync(Guid id, UpdateDoctorRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<NurseStaffDto>> GetNursesAsync(CancellationToken cancellationToken);
    Task<NurseStaffDto> UpdateNurseAsync(Guid id, UpdateNurseRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<LabTestDto>> GetLabTestsAsync(CancellationToken cancellationToken);
    Task<LabTestDto> CreateLabTestAsync(SaveLabTestRequest request, CancellationToken cancellationToken);
    Task<LabTestDto> UpdateLabTestAsync(Guid id, SaveLabTestRequest request, CancellationToken cancellationToken);
}
