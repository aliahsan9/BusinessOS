using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Onboarding.DTOs;
using BusinessOS.Application.Features.Onboarding.Services;
using BusinessOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class OnboardingService : IOnboardingService
{
    private const int TotalSteps = 8;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public OnboardingService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<OnboardingStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var progress = await GetOrCreateProgressAsync(cancellationToken);
        return MapStatus(progress);
    }

    public async Task<OnboardingStatusDto> SaveProgressAsync(
        SaveOnboardingProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        var progress = await GetOrCreateProgressAsync(cancellationToken);

        progress.CurrentStep = Math.Clamp(request.CurrentStep, 1, TotalSteps);
        progress.IsSkipped = request.IsSkipped;
        progress.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapStatus(progress);
    }

    public async Task<OnboardingStatusDto> CompleteAsync(CancellationToken cancellationToken = default)
    {
        var progress = await GetOrCreateProgressAsync(cancellationToken);

        progress.CurrentStep = TotalSteps;
        progress.IsCompleted = true;
        progress.IsSkipped = false;
        progress.CompletedAt = DateTime.UtcNow;
        progress.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapStatus(progress);
    }

    public async Task<OnboardingBusinessProfileDto> GetBusinessProfileAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new BadRequestException("Tenant context is required.");

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");

        var settings = await _context.TenantSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        return new OnboardingBusinessProfileDto
        {
            Name = tenant.Name,
            LogoUrl = settings?.LogoUrl,
            Website = tenant.Website,
            Industry = tenant.BusinessType,
            Description = tenant.Description,
            Currency = settings?.Currency ?? "USD",
            Timezone = settings?.Timezone ?? "UTC"
        };
    }

    public async Task SaveBusinessProfileAsync(
        SaveOnboardingBusinessProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new BadRequestException("Tenant context is required.");

        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");

        tenant.Name = request.Name.Trim();
        tenant.BusinessType = request.Industry.Trim();
        tenant.Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();
        tenant.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        tenant.UpdatedAt = DateTime.UtcNow;

        var settings = await _context.TenantSettings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (settings is null)
        {
            settings = new TenantSettings
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId
            };
            _context.TenantSettings.Add(settings);
        }

        settings.Currency = request.Currency.Trim();
        settings.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "UTC" : request.Timezone.Trim();
        settings.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<UserOnboardingProgress> GetOrCreateProgressAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedException("User context is required.");

        var progress = await _context.UserOnboardingProgress
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (progress is not null)
            return progress;

        progress = new UserOnboardingProgress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CurrentStep = 1
        };

        _context.UserOnboardingProgress.Add(progress);
        await _context.SaveChangesAsync(cancellationToken);

        return progress;
    }

    private static OnboardingStatusDto MapStatus(UserOnboardingProgress progress) =>
        new()
        {
            CurrentStep = progress.CurrentStep,
            IsCompleted = progress.IsCompleted,
            IsSkipped = progress.IsSkipped,
            CompletedAt = progress.CompletedAt,
            ShouldShowWizard = !progress.IsCompleted && !progress.IsSkipped
        };
}
