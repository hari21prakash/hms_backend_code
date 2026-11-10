namespace HospitalManagement.Api.Models;

/// <summary>Common columns shared by all persisted entities. Timestamps are stored in UTC.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
