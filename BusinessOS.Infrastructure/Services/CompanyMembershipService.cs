using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Application.Features.Platform;
using BusinessOS.Application.Features.Tenant.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class CompanyMembershipService : ICompanyMembershipService
{
    private readonly BusinessOSDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IRoleRepository _roles;
    private readonly IJwtTokenGenerator _tokens;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantAuditService _audit;
    private readonly IHttpContextAccessor _http;

    public CompanyMembershipService(
        BusinessOSDbContext db,
        UserManager<ApplicationUser> users,
        IRoleRepository roles,
        IJwtTokenGenerator tokens,
        ICurrentUserService currentUser,
        ITenantAuditService audit,
        IHttpContextAccessor http)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _tokens = tokens;
        _currentUser = currentUser;
        _audit = audit;
        _http = http;
    }

    public async Task<IReadOnlyList<CompanyMembershipDto>> ListMineAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        await EnsureLegacyMembershipAsync(userId, cancellationToken);

        var user = await _users.FindByIdAsync(userId)
            ?? throw new UnauthorizedException("Account was not found.");

        var memberships = await _db.CompanyMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive && !x.IsDeleted)
            .Join(
                _db.Tenants.IgnoreQueryFilters().Where(t => !t.IsDeleted),
                membership => membership.TenantId,
                tenant => tenant.Id,
                (membership, tenant) => new CompanyMembershipDto
                {
                    CompanyId = tenant.Id,
                    CompanyName = tenant.Name,
                    Role = membership.RoleName,
                    Status = tenant.Status.ToString(),
                    IsCurrent = tenant.Id == user.TenantId
                })
            .OrderByDescending(x => x.IsCurrent)
            .ThenBy(x => x.CompanyName)
            .ToListAsync(cancellationToken);

        return memberships;
    }

    public async Task<AuthResponse> SwitchAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            throw new BadRequestException("A company is required.");

        var userId = RequireUserId();
        var user = await _users.FindByIdAsync(userId)
            ?? throw new UnauthorizedException("Account was not found.");

        var membership = await _db.CompanyMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.TenantId == companyId && x.IsActive && !x.IsDeleted,
                cancellationToken);

        if (membership is null && user.TenantId == companyId)
        {
            membership = await EnsureLegacyMembershipAsync(userId, cancellationToken);
        }

        if (membership is null || membership.TenantId != companyId)
            throw new ForbiddenException("You are not a member of this company.");

        var company = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == companyId && !x.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Company was not found.");

        if (user.TenantId != companyId)
        {
            _db.AllowUserTenantReassignment = true;
            try
            {
                user.TenantId = companyId;
                user.SecurityVersion++;
                await ReplaceActiveRoleAsync(user, membership.RoleName, cancellationToken);
                var update = await _users.UpdateAsync(user);
                if (!update.Succeeded)
                    throw new BadRequestException(string.Join(", ", update.Errors.Select(x => x.Description)));
            }
            finally
            {
                _db.AllowUserTenantReassignment = false;
            }

            await _audit.LogAsync(
                companyId,
                "CompanySwitched",
                "CompanyMembership",
                null,
                company.Name,
                userId,
                ClientIp(),
                cancellationToken);
        }

        return await IssueTokenAsync(user, cancellationToken);
    }

    public async Task TransferOwnershipAsync(string newOwnerUserId, CancellationToken cancellationToken = default)
    {
        var actorId = RequireUserId();
        if (!_currentUser.Roles.Contains(RoleNames.Owner, StringComparer.OrdinalIgnoreCase))
            throw new ForbiddenException("Only the company owner can transfer ownership.");

        if (string.IsNullOrWhiteSpace(newOwnerUserId) || newOwnerUserId == actorId)
            throw new BadRequestException("Choose a different member to become the owner.");

        var companyId = _currentUser.TenantId
            ?? throw new BadRequestException("Company context is required.");

        var company = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == companyId, cancellationToken)
            ?? throw new NotFoundException("Company was not found.");

        if (!string.Equals(company.OwnerUserId, actorId, StringComparison.Ordinal))
            throw new ForbiddenException("Only the company owner can transfer ownership.");

        var targetMembership = await _db.CompanyMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.UserId == newOwnerUserId && x.TenantId == companyId && x.IsActive && !x.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException("That person is not an active member of this company.");

        var actorMembership = await _db.CompanyMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.UserId == actorId && x.TenantId == companyId && !x.IsDeleted,
                cancellationToken);

        var target = await _users.FindByIdAsync(newOwnerUserId)
            ?? throw new NotFoundException("That member was not found.");
        var actor = await _users.FindByIdAsync(actorId)
            ?? throw new UnauthorizedException("Account was not found.");

        if (!target.IsActive)
            throw new BadRequestException("An inactive member cannot become the owner.");

        targetMembership.RoleName = RoleNames.Owner;
        targetMembership.UpdatedAt = DateTime.UtcNow;
        if (actorMembership is not null)
        {
            actorMembership.RoleName = RoleNames.Admin;
            actorMembership.IsActive = true;
            actorMembership.UpdatedAt = DateTime.UtcNow;
        }

        company.OwnerUserId = target.Id;
        company.UpdatedAt = DateTime.UtcNow;

        _db.AllowUserTenantReassignment = true;
        try
        {
            if (actor.TenantId == companyId)
            {
                actor.SecurityVersion++;
                await ReplaceActiveRoleAsync(actor, RoleNames.Admin, cancellationToken);
                var actorUpdate = await _users.UpdateAsync(actor);
                if (!actorUpdate.Succeeded)
                    throw new BadRequestException(string.Join(", ", actorUpdate.Errors.Select(x => x.Description)));
            }

            if (target.TenantId == companyId)
            {
                target.SecurityVersion++;
                await ReplaceActiveRoleAsync(target, RoleNames.Owner, cancellationToken);
                var targetUpdate = await _users.UpdateAsync(target);
                if (!targetUpdate.Succeeded)
                    throw new BadRequestException(string.Join(", ", targetUpdate.Errors.Select(x => x.Description)));
            }
        }
        finally
        {
            _db.AllowUserTenantReassignment = false;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(
            companyId,
            "OwnershipTransferred",
            "Tenant",
            actorId,
            target.Id,
            actorId,
            ClientIp(),
            cancellationToken);
    }

    public async Task EnsureOwnerMembershipAsync(Guid tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.CompanyMemberships
            .IgnoreQueryFilters()
            .AnyAsync(x => x.TenantId == tenantId && x.UserId == userId && !x.IsDeleted, cancellationToken);

        if (exists)
            return;

        _db.CompanyMemberships.Add(new CompanyMembership
        {
            TenantId = tenantId,
            UserId = userId,
            RoleName = RoleNames.Owner,
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<CompanyMembership?> EnsureLegacyMembershipAsync(string userId, CancellationToken cancellationToken)
    {
        var existing = await _db.CompanyMemberships
            .IgnoreQueryFilters()
            .Where(x => x.UserId == userId && x.IsActive && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            return existing.FirstOrDefault(x => x.TenantId == _currentUser.TenantId) ?? existing[0];

        var user = await _users.FindByIdAsync(userId);
        if (user is null || user.TenantId == Guid.Empty)
            return null;

        var role = _currentUser.Roles.FirstOrDefault(name => RoleNames.All.Contains(name)) ?? RoleNames.Employee;
        var membership = new CompanyMembership
        {
            TenantId = user.TenantId,
            UserId = userId,
            RoleName = role,
            IsActive = true,
            JoinedAt = user.JoinedAt == default ? DateTime.UtcNow : user.JoinedAt
        };
        _db.CompanyMemberships.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);
        return membership;
    }

    private async Task ReplaceActiveRoleAsync(ApplicationUser user, string roleName, CancellationToken cancellationToken)
    {
        if (!RoleNames.All.Contains(roleName))
            throw new BadRequestException("That company role is not recognized.");

        var identityRoles = await _users.GetRolesAsync(user);
        var removable = identityRoles.Where(role => RoleNames.All.Contains(role)).ToList();
        if (removable.Count > 0)
        {
            var removed = await _users.RemoveFromRolesAsync(user, removable);
            if (!removed.Succeeded)
                throw new BadRequestException(string.Join(", ", removed.Errors.Select(x => x.Description)));
        }

        if (!await _users.IsInRoleAsync(user, roleName))
        {
            var added = await _users.AddToRoleAsync(user, roleName);
            if (!added.Succeeded)
                throw new BadRequestException(string.Join(", ", added.Errors.Select(x => x.Description)));
        }

        var current = await _db.RbacUserRoles.Where(x => x.UserId == user.Id).ToListAsync(cancellationToken);
        if (current.Count > 0)
            _db.RbacUserRoles.RemoveRange(current);

        var role = await _roles.GetRoleByNameAsync(roleName, cancellationToken)
            ?? throw new BadRequestException($"Role {roleName} is not configured.");
        _db.RbacUserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueTokenAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = await _roles.GetUserRoleNamesAsync(user.Id, cancellationToken);
        var permissions = await _roles.GetUserPermissionCodesAsync(user.Id, cancellationToken);
        var version = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == user.Id)
            .Select(x => x.SecurityVersion)
            .FirstAsync(cancellationToken);

        return new AuthResponse
        {
            Token = _tokens.GenerateToken(user.Id, user.Email ?? string.Empty, user.TenantId, roles, permissions, version),
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            TenantId = user.TenantId,
            Roles = roles,
            Permissions = permissions,
            ExpiresAt = _tokens.GetTokenExpiration()
        };
    }

    private string RequireUserId() =>
        _currentUser.UserId ?? throw new UnauthorizedException("Authentication is required.");

    private string? ClientIp()
    {
        var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? null : ip.Length <= 64 ? ip : ip[..64];
    }
}
