using BusinessOS.Application.Features.Organization.DTOs;

namespace BusinessOS.Application.Features.Organization.Services;

public interface IOrganizationService
{
    Task<OrganizationDto> GetOrganizationAsync(CancellationToken cancellationToken = default);

    Task<OrganizationDto> UpdateOrganizationAsync(
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default);
}
