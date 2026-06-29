using BusinessOS.Application.Features.Help.DTOs;

namespace BusinessOS.Application.Features.Help.Services;

public interface IHelpService
{
    Task<HelpCenterDto> GetHelpCenterAsync(CancellationToken cancellationToken = default);
}
