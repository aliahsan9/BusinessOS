using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Employee record for a tenant's internal workforce and payroll tracking.
/// </summary>
/// <remarks>
/// Tenant-scoped. Stores contact information, designation, salary, and employment status.
/// </remarks>
public class Employee : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant that employs this person. Required for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Employee's first name. Required.
    /// </summary>
    public string FirstName { get; set; } = default!;

    /// <summary>
    /// Employee's last name. Required.
    /// </summary>
    public string LastName { get; set; } = default!;

    /// <summary>
    /// Work email address for the employee. Required.
    /// </summary>
    public string Email { get; set; } = default!;

    /// <summary>
    /// Work phone number for the employee. Required.
    /// </summary>
    public string Phone { get; set; } = default!;

    /// <summary>
    /// Job title or role designation within the organization. Required.
    /// </summary>
    public string Designation { get; set; } = default!;

    public string? EmployeeCode { get; set; }
    public string? UserId { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public Guid? PrimaryBranchId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? ManagerEmployeeId { get; set; }
    public bool IsRegionalManager { get; set; }
    public string EmploymentStatus { get; set; } = "Active";
    public string EmploymentType { get; set; } = "FullTime";
    public string? Skills { get; set; }
    public string? WorkLocation { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    /// <summary>
    /// Current salary amount in the tenant's currency. Must be non-negative.
    /// </summary>
    public decimal Salary { get; set; }

    /// <summary>
    /// Date the employee joined the organization. Required.
    /// </summary>
    public DateTime JoiningDate { get; set; }

    /// <summary>
    /// Indicates whether the employee is currently active. Defaults to true.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
