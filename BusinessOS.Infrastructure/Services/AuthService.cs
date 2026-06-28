using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Interfaces;
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

    public AuthService(
        IIdentityService identityService,
        ITenantRegistrationService tenantRegistrationService,
        IJwtTokenGenerator jwtTokenGenerator,
        ITenantProvider tenantProvider,
        IDbContextFactory<BusinessOSDbContext> dbContextFactory)
    {
        _identityService = identityService;
        _tenantRegistrationService = tenantRegistrationService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _tenantProvider = tenantProvider;
        _dbContextFactory = dbContextFactory;
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

        var roles = await _identityService.GetRolesAsync(user, cancellationToken);
        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.Email, user.TenantId, roles);

        return new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            TenantId = user.TenantId,
            ExpiresAt = _jwtTokenGenerator.GetTokenExpiration()
        };
    }

    public async Task<AuthResponse> RegisterAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string businessName,
        CancellationToken cancellationToken)
    {
        var existingUser = await _identityService.FindByEmailAsync(email, cancellationToken);
        if (existingUser is not null)
            throw new ConflictException("A user with this email already exists.");

        var tenantId = Guid.NewGuid();
        _tenantProvider.SetTenantId(tenantId);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await _tenantRegistrationService.CreateTenantAsync(
            tenantId,
            businessName,
            email,
            "pending",
            cancellationToken);

        var createResult = await _identityService.CreateUserAsync(
            new CreateUserRequest(email, password, firstName, lastName, tenantId),
            cancellationToken);

        if (!createResult.Succeeded)
            throw new BadRequestException(string.Join(", ", createResult.Errors));

        var user = await _identityService.FindByEmailAsync(email, cancellationToken)
            ?? throw new BadRequestException("User registration failed.");

        await _identityService.AddToRoleAsync(user, "Admin", cancellationToken);

        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == tenantId, cancellationToken);

        tenant.OwnerUserId = user.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await _identityService.GetRolesAsync(user, cancellationToken);
        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.Email, user.TenantId, roles);

        return new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            TenantId = user.TenantId,
            ExpiresAt = _jwtTokenGenerator.GetTokenExpiration()
        };
    }
}
