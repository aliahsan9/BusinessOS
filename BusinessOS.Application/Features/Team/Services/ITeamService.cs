using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Team.DTOs;

namespace BusinessOS.Application.Features.Team.Services;

public interface ITeamService
{
    Task<TeamDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<TeamMemberDto>> GetMembersAsync(
        string? search,
        string? status,
        string? role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<TeamMemberDto> GetMemberByIdAsync(
        string memberId,
        CancellationToken cancellationToken = default);

    Task<TeamInvitationDto> InviteMemberAsync(
        InviteTeamMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<TeamMemberDto> UpdateMemberAsync(
        string memberId,
        UpdateTeamMemberRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        string memberId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeamActivityDto>> GetTeamActivityAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TeamInvitationDto>> GetPendingInvitationsAsync(
        CancellationToken cancellationToken = default);

    Task<InvitationPreviewDto> GetInvitationPreviewAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task AcceptInvitationAsync(
        AcceptInvitationRequest request,
        CancellationToken cancellationToken = default);
}
