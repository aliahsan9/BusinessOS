using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Auth.DTOs;

namespace BusinessOS.Application.Features.Platform;

public interface IBusinessTypeCatalogService
{
    Task<IReadOnlyList<BusinessTypeTemplateDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<string?> ModulesJsonForKeyAsync(string? key, CancellationToken cancellationToken = default);
}

public interface ICompanyMembershipService
{
    Task<IReadOnlyList<CompanyMembershipDto>> ListMineAsync(CancellationToken cancellationToken = default);
    Task<AuthResponse> SwitchAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task TransferOwnershipAsync(string newOwnerUserId, CancellationToken cancellationToken = default);
    Task EnsureOwnerMembershipAsync(Guid tenantId, string userId, CancellationToken cancellationToken = default);
}

public interface ICompanyFileStore
{
    Task<IReadOnlyList<StoredFileDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<StoredFileDto> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<(Stream Content, StoredFileDto File)> OpenAsync(Guid fileId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default);
}

public interface ICompanyKnowledgeService
{
    Task<IReadOnlyList<KnowledgeDocumentDto>> ListAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<CompanyResourceDto> GetResourcesAsync(CancellationToken cancellationToken = default);
}

public interface ICompanyAnnouncementService
{
    Task<IReadOnlyList<AnnouncementDto>> ListVisibleAsync(CancellationToken cancellationToken = default);
}

public interface IPlatformAdminService
{
    PlatformSessionDto GetSession();
    Task<PagedResult<CompanySummaryDto>> ListCompaniesAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<CompanyProfileDto> GetCompanyAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<CompanyProfileDto> SetCompanyStatusAsync(Guid companyId, string status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformSubscriptionRowDto>> ListSubscriptionsAsync(CancellationToken cancellationToken = default);
    Task<PlatformRevenueDto> GetRevenueAsync(CancellationToken cancellationToken = default);
    Task<AiUsageReportDto> GetAiUsageAsync(Guid? companyId, string? operation, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<PagedResult<AgentExecutionDto>> ListAgentExecutionsAsync(Guid? companyId, string? agent, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<AgentExecutionDetailDto> GetAgentExecutionAsync(Guid executionId, CancellationToken cancellationToken = default);
    Task<PlatformHealthDto> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformSettingDto>> GetAiSettingsAsync(CancellationToken cancellationToken = default);
    Task<PlatformSettingDto> UpsertAiSettingAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AnnouncementDto>> ListAnnouncementsAsync(CancellationToken cancellationToken = default);
    Task<AnnouncementDto> SaveAnnouncementAsync(Guid? id, SaveAnnouncementRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAnnouncementAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPlatformAiOverride
{
    Task<string> ChatModelAsync(string fallback, CancellationToken cancellationToken = default);
    Task<string> EmbeddingModelAsync(string fallback, CancellationToken cancellationToken = default);
}
