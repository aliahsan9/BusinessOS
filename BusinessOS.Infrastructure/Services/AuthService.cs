using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessOS.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly IIdentityService _identityService;
    private readonly ITenantRegistrationService _tenantRegistrationService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ITenantProvider _tenantProvider;
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;
    private readonly IRoleRepository _roleRepository;
    private readonly IRbacAuditService _auditService;

    public AuthService(
        IIdentityService identityService,
        ITenantRegistrationService tenantRegistrationService,
        IJwtTokenGenerator jwtTokenGenerator,
        ITenantProvider tenantProvider,
        IDbContextFactory<BusinessOSDbContext> dbContextFactory,
        IRoleRepository roleRepository,
        IRbacAuditService auditService)
    {
        _identityService = identityService;
        _tenantRegistrationService = tenantRegistrationService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _tenantProvider = tenantProvider;
        _dbContextFactory = dbContextFactory;
        _roleRepository = roleRepository;
        _auditService = auditService;
    }

    public async Task<AuthResponse> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await _identityService.FindByEmailAsync(email, cancellationToken);

        if (user is null ||
            !await _identityService.ValidatePasswordAsync(user, password, cancellationToken))
        {
            throw new UnauthorizedException("Invalid credentials");
        }

        _tenantProvider.SetTenantId(user.TenantId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var appUser = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == user.Id, cancellationToken);
        if (appUser is not null)
        {
            if (!appUser.IsActive)
            {
                throw new UnauthorizedException("Account is deactivated.");
            }

            appUser.LastActiveAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var roles = await _roleRepository.GetUserRoleNamesAsync(user.Id, cancellationToken);
        if (roles.Count == 0)
        {
            roles = await _identityService.GetRolesAsync(user, cancellationToken);
        }

        var permissions = await _roleRepository.GetUserPermissionCodesAsync(user.Id, cancellationToken);
        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.Email, user.TenantId, roles, permissions);

        await _auditService.LogAsync(
            "UserLogin",
            "User",
            user.Id,
            null,
            RbacAuditService.Serialize(new { user.Email }),
            cancellationToken);

        return new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            TenantId = user.TenantId,
            Roles = roles,
            Permissions = permissions,
            ExpiresAt = _jwtTokenGenerator.GetTokenExpiration()
        };
    }

    public async Task<AuthResponse> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string businessName,
        CancellationToken cancellationToken,
        string timezone = "UTC",
        string currency = "USD",
        string industry = "General")
    {
        var existingUser = await _identityService.FindByEmailAsync(email, cancellationToken);
        if (existingUser is not null)
            throw new ConflictException("A user with this email already exists.");

        var tenantId = Guid.NewGuid();
        _tenantProvider.SetTenantId(tenantId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await _tenantRegistrationService.CreateTenantAsync(
            new CreateTenantOptions(
                tenantId,
                businessName,
                email,
                "pending",
                timezone,
                currency,
                industry),
            cancellationToken);

        var createResult = await _identityService.CreateUserAsync(
            new CreateUserRequest(email, password, firstName, lastName, tenantId),
            cancellationToken);

        if (!createResult.Succeeded)
            throw new BadRequestException(string.Join(", ", createResult.Errors));

        var user = await _identityService.FindByEmailAsync(email, cancellationToken)
            ?? throw new BadRequestException("User registration failed.");

        await _identityService.AddToRoleAsync(user, RoleNames.Owner, cancellationToken);
        await AssignRbacRoleAsync(user.Id, RoleNames.Owner, cancellationToken);

        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == tenantId, cancellationToken);

        tenant.OwnerUserId = user.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await _roleRepository.GetUserRoleNamesAsync(user.Id, cancellationToken);
        var permissions = await _roleRepository.GetUserPermissionCodesAsync(user.Id, cancellationToken);
        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.Email, user.TenantId, roles, permissions);

        return new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            TenantId = user.TenantId,
            Roles = roles,
            Permissions = permissions,
            ExpiresAt = _jwtTokenGenerator.GetTokenExpiration()
        };
    }

    private async Task AssignRbacRoleAsync(string userId, string roleName, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetRoleByNameAsync(roleName, cancellationToken);
        if (role is not null)
        {
            await _roleRepository.AssignRoleToUserAsync(userId, role.Id, cancellationToken);
        }
    }
}
