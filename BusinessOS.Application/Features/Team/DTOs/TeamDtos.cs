namespace BusinessOS.Application.Features.Team.DTOs;

public class TeamMemberDto
{
    public string Id { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];
    public string? PrimaryRole { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public string Status => IsActive ? "Active" : "Inactive";
}

public class TeamDashboardDto
{
    public int TotalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public int PendingInvitations { get; set; }
    public IReadOnlyList<RoleDistributionDto> RoleDistribution { get; set; } = [];
    public IReadOnlyList<TeamActivityDto> RecentActivity { get; set; } = [];
    public IReadOnlyList<AssignedTaskSummaryDto> AssignedTasks { get; set; } = [];
}

public class RoleDistributionDto
{
    public string RoleName { get; set; } = default!;
    public int Count { get; set; }
}

public class TeamActivityDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

public class AssignedTaskSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string ProjectName { get; set; } = default!;
    public string? AssignedUserName { get; set; }
    public string Status { get; set; } = default!;
    public DateTime? DueDate { get; set; }
}

public class TeamInvitationDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string RoleName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record InviteTeamMemberRequest(
    string Email,
    Guid RoleId,
    string? FirstName = null,
    string? LastName = null);

public record UpdateTeamMemberRequest(
    string FirstName,
    string LastName,
    string? AvatarUrl,
    Guid? RoleId,
    bool IsActive);

public record AcceptInvitationRequest(
    string Token,
    string Password,
    string FirstName,
    string LastName);

public record InvitationPreviewDto(
    string Email,
    string OrganizationName,
    string RoleName,
    DateTime ExpiresAt,
    bool IsValid);
