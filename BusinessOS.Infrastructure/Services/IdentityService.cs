using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Account.DTOs;
using BusinessOS.Application.Features.Users.DTOs;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BusinessOS.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        ILogger<IdentityService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<UserAuthResult?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user is null ? null : MapUser(user);
    }

    public Task<bool> ValidatePasswordAsync(
        UserAuthResult user,
        string password,
        CancellationToken cancellationToken) =>
        ValidatePasswordInternalAsync(user.Id, password);

    private async Task<bool> ValidatePasswordInternalAsync(string userId, string password)
    {
        var appUser = await _userManager.FindByIdAsync(userId);
        return appUser is not null && await _userManager.CheckPasswordAsync(appUser, password);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(
        UserAuthResult user,
        CancellationToken cancellationToken)
    {
        var appUser = await _userManager.FindByIdAsync(user.Id)
            ?? throw new InvalidOperationException("User not found.");

        return (IReadOnlyList<string>)await _userManager.GetRolesAsync(appUser);
    }

    public async Task<IdentityOperationResult> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            TenantId = request.TenantId,
            JoinedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        return new IdentityOperationResult(
            result.Succeeded,
            result.Errors.Select(x => x.Description).ToList());
    }

    public async Task AddToRoleAsync(
        UserAuthResult user,
        string role,
        CancellationToken cancellationToken)
    {
        var appUser = await _userManager.FindByIdAsync(user.Id)
            ?? throw new InvalidOperationException("User not found.");

        await _userManager.AddToRoleAsync(appUser, role);
    }

    public async Task<PagedResult<UserSummaryResponse>> GetUsersAsync(
        Guid tenantId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (normalizedPage, normalizedPageSize) = PaginationParams.Normalize(page, pageSize);

        var query = _userManager.Users
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(x =>
                x.Email!.Contains(normalizedSearch) ||
                x.FirstName.Contains(normalizedSearch) ||
                x.LastName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var items = new List<UserSummaryResponse>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new UserSummaryResponse
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                IsActive = user.IsActive,
                Roles = roles.ToList()
            });
        }

        return new PagedResult<UserSummaryResponse>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task<UserResponse> GetUserByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        return await MapUserResponseAsync(user);
    }

    public async Task<UserResponse> UpdateUserAsync(
        string userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        user.Email = request.Email.Trim();
        user.UserName = request.Email.Trim();
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.IsActive = request.IsActive;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new BadRequestException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }

        return await MapUserResponseAsync(user);
    }

    public async Task DeactivateUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new BadRequestException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }

    public async Task ActivateUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        user.IsActive = true;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new BadRequestException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }
    }

    public async Task<IdentityOperationResult> ResetPasswordAsync(
        string userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

        if (result.Succeeded)
        {
            _logger.LogInformation("Password reset completed for user {UserId}", userId);
        }
        else
        {
            _logger.LogWarning(
                "Password reset failed for user {UserId}: {Errors}",
                userId,
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return new IdentityOperationResult(
            result.Succeeded,
            result.Errors.Select(x => x.Description).ToList());
    }

    public async Task<string?> GeneratePasswordResetTokenAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive)
        {
            return null;
        }

        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<IdentityOperationResult> ResetPasswordWithTokenAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return new IdentityOperationResult(false, ["Invalid or expired password reset link."]);
        }

        if (!user.IsActive)
        {
            return new IdentityOperationResult(false, ["Account is deactivated."]);
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (result.Succeeded)
        {
            _logger.LogInformation("Password reset via token completed for user {UserId}", user.Id);
        }
        else
        {
            _logger.LogWarning(
                "Password reset via token failed for {Email}: {Errors}",
                email,
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return new IdentityOperationResult(
            result.Succeeded,
            result.Succeeded
                ? Array.Empty<string>()
                : result.Errors.Select(x => x.Description).ToList());
    }

    public async Task<AccountProfileResponse> GetAccountProfileAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        return await MapAccountProfileAsync(user);
    }

    public async Task<AccountProfileResponse> UpdateAccountProfileAsync(
        string userId,
        UpdateAccountProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        var email = request.Email.Trim();
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing is not null && existing.Id != userId)
            {
                throw new BadRequestException("An account with this email already exists.");
            }
        }

        user.Email = email;
        user.UserName = email;
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber)
            ? null
            : request.PhoneNumber.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl)
            ? null
            : request.AvatarUrl.Trim();
        user.LastActiveAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new BadRequestException(string.Join("; ", result.Errors.Select(x => x.Description)));
        }

        return await MapAccountProfileAsync(user);
    }

    public async Task<IdentityOperationResult> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return new IdentityOperationResult(
            result.Succeeded,
            result.Errors.Select(x => x.Description).ToList());
    }

    private async Task<UserResponse> MapUserResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new UserResponse
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            TenantId = user.TenantId,
            IsActive = user.IsActive,
            Roles = roles.ToList()
        };
    }

    private async Task<AccountProfileResponse> MapAccountProfileAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        return new AccountProfileResponse
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            TenantId = user.TenantId,
            IsActive = user.IsActive,
            Roles = roles.ToList(),
            JoinedAt = user.JoinedAt,
            LastActiveAt = user.LastActiveAt
        };
    }

    private static UserAuthResult MapUser(ApplicationUser user) =>
        new(user.Id, user.Email!, user.TenantId);
}
