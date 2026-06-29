namespace BusinessOS.Application.Features.Organization.DTOs;

public class OrganizationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string BusinessType { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string? Website { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string Currency { get; set; } = "USD";
    public string Timezone { get; set; } = "UTC";
    public string SubscriptionPlan { get; set; } = "Free";
    public bool IsActive { get; set; }
}

public record UpdateOrganizationRequest(
    string Name,
    string BusinessType,
    string Email,
    string Phone,
    string Address,
    string? Website,
    string? Description,
    string? LogoUrl,
    string Currency,
    string Timezone);
