using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Activities.Services;
using BusinessOS.Application.Features.Auth;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly IActivityService _activityService;
    private readonly IEmailNotificationService _emailService;
    private readonly IMemoryCache _resetAttempts;
    private readonly AppOptions _appOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IIdentityService identityService,
        ITenantRegistrationService tenantRegistrationService,
        IJwtTokenGenerator jwtTokenGenerator,
        ITenantProvider tenantProvider,
        IDbContextFactory<BusinessOSDbContext> dbContextFactory,
        IRoleRepository roleRepository,
        IRbacAuditService auditService,
        IActivityService activityService,
        IEmailNotificationService emailService,
        IOptions<AppOptions> appOptions,
        IMemoryCache resetAttempts,
        ILogger<AuthService> logger)
    {
        _identityService = identityService;
        _tenantRegistrationService = tenantRegistrationService;
        _jwtTokenGenerator = jwtTokenGenerator;
        _tenantProvider = tenantProvider;
        _dbContextFactory = dbContextFactory;
        _roleRepository = roleRepository;
        _auditService = auditService;
        _activityService = activityService;
        _emailService = emailService;
        _appOptions = appOptions.Value;
        _resetAttempts = resetAttempts;
        _logger = logger;
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
            _logger.LogWarning("Failed login attempt for {Email}", email);
            throw new UnauthorizedException("Invalid credentials");
        }

        return await CreateSessionForUserAsync(user, cancellationToken);
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

        _logger.LogInformation(
            "Tenant {TenantId} created for business {BusinessName}",
            tenantId,
            businessName);

        var createResult = await _identityService.CreateUserAsync(
            new CreateUserRequest(email, password, firstName, lastName, tenantId),
            cancellationToken);

        if (!createResult.Succeeded)
            throw new BadRequestException(string.Join(", ", createResult.Errors));

        var user = await _identityService.FindByEmailAsync(email, cancellationToken)
            ?? throw new BadRequestException("User registration failed.");

        return await FinalizeOwnerRegistrationAsync(user, tenantId, dbContext, cancellationToken);
    }

    public async Task<AuthResponse> RegisterExternalUserAsync(
        string email,
        string firstName,
        string lastName,
        string businessName,
        string loginProvider,
        string providerKey,
        string? avatarUrl,
        CancellationToken cancellationToken)
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
                "UTC",
                "PKR",
                "General"),
            cancellationToken);

        var createResult = await _identityService.CreateExternalUserAsync(
            new CreateExternalUserRequest(
                email,
                firstName,
                lastName,
                tenantId,
                loginProvider,
                providerKey,
                avatarUrl),
            cancellationToken);

        if (!createResult.Succeeded)
            throw new BadRequestException(string.Join(", ", createResult.Errors));

        var user = await _identityService.FindByEmailAsync(email, cancellationToken)
            ?? throw new BadRequestException("User registration failed.");

        _logger.LogInformation(
            "External registration created tenant {TenantId} for provider {Provider}",
            tenantId,
            loginProvider);

        return await FinalizeOwnerRegistrationAsync(user, tenantId, dbContext, cancellationToken);
    }

    public async Task<AuthResponse> CreateSessionForUserAsync(
        UserAuthResult user,
        CancellationToken cancellationToken,
        string auditAction = "UserLogin")
    {
        if (await _identityService.IsLockedOutAsync(user.Id, cancellationToken))
        {
            _logger.LogWarning("Login blocked for locked out user {UserId}", user.Id);
            throw new UnauthorizedException("Account is temporarily locked.");
        }

        _tenantProvider.SetTenantId(user.TenantId);
        var securityVersion = 1;

        var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (dbContext is not null)
        {
            await using (dbContext)
            {
                var appUser = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == user.Id, cancellationToken);
                if (appUser is not null)
                {
                    if (!appUser.IsActive)
                    {
                        _logger.LogWarning(
                            "Login blocked for deactivated user {UserId}",
                            user.Id);
                        throw new UnauthorizedException("Account is deactivated.");
                    }

                    securityVersion = appUser.SecurityVersion;
                    appUser.LastActiveAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }

        var roles = await _roleRepository.GetUserRoleNamesAsync(user.Id, cancellationToken);
        if (roles.Count == 0)
        {
            roles = await _identityService.GetRolesAsync(user, cancellationToken);
        }

        var permissions = await _roleRepository.GetUserPermissionCodesAsync(user.Id, cancellationToken);
        var token = _jwtTokenGenerator.GenerateToken(
            user.Id, user.Email, user.TenantId, roles, permissions, securityVersion);

        _logger.LogInformation(
            "User {UserId} signed in for Tenant {TenantId}. AuditAction={AuditAction}",
            user.Id,
            user.TenantId,
            auditAction);

        await _auditService.LogAsync(
            auditAction,
            "User",
            user.Id,
            null,
            RbacAuditService.Serialize(new { user.Email }),
            cancellationToken);

        var userName = user.Email.Split('@')[0];
        var entityId = Guid.TryParse(user.Id, out var parsedUserId) ? parsedUserId : Guid.Empty;
        try
        {
            await _activityService.LogForUserAsync(
                user.Id,
                userName,
                new LogActivityRequest(
                    ActivityActions.Login,
                    "User",
                    entityId,
                    userName,
                    RbacAuditService.Serialize(new { user.Email })),
                cancellationToken);
        }
        catch
        {
            // Activity logging should not block authentication.
        }

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

    public async Task<PasswordResetResponse> ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim();
        if (!TryConsumeResetRequest(normalizedEmail))
        {
            _logger.LogWarning("Password reset request throttled for an email hash.");
            return new PasswordResetResponse { Message = PasswordResetMessages.PublicSuccessMessage };
        }

        var token = await _identityService.GeneratePasswordResetTokenAsync(normalizedEmail, cancellationToken);
        if (token is null)
        {
            _logger.LogInformation(
                "Password reset request did not produce a token for {Email}",
                normalizedEmail);
            return new PasswordResetResponse { Message = PasswordResetMessages.PublicSuccessMessage };
        }

        try
        {
            var resetUrl = PasswordResetLink.Build(_appOptions.FrontendBaseUrl, normalizedEmail, token);
            var hours = Math.Clamp(_appOptions.PasswordResetTokenHours, 1, 168);
            var delivery = await _emailService.SendMessageAsync(
                PasswordResetEmail.Compose(normalizedEmail, resetUrl, hours),
                cancellationToken);

            if (!delivery.Delivered)
            {
                _logger.LogError(
                    "Password reset email was not delivered. ProviderConfigured={ProviderConfigured}",
                    delivery.ProviderConfigured);
            }
            else
            {
                _logger.LogInformation("Password reset email accepted by the provider.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                "Password reset email delivery failed. ErrorType={ErrorType} Detail={Detail}",
                ex.GetType().Name,
                SafeEmailError(ex));
        }

        return new PasswordResetResponse { Message = PasswordResetMessages.PublicSuccessMessage };
    }

    public async Task<PasswordResetResponse> ResetPasswordAsync(
        string email,
        string token,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var result = await _identityService.ResetPasswordWithTokenAsync(
            email.Trim(),
            token,
            newPassword,
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new BadRequestException(string.Join("; ", PasswordResetMessages.PublicErrors(result.Errors)));
        }

        _logger.LogInformation("Password reset completed.");
        return new PasswordResetResponse { Message = PasswordResetMessages.ResetSucceededMessage };
    }

    private async Task<AuthResponse> FinalizeOwnerRegistrationAsync(
        UserAuthResult user,
        Guid tenantId,
        BusinessOSDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await _identityService.AddToRoleAsync(user, RoleNames.Owner, cancellationToken);
        await AssignRbacRoleAsync(user.Id, RoleNames.Owner, cancellationToken);

        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstAsync(x => x.Id == tenantId, cancellationToken);

        tenant.OwnerUserId = user.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = await _roleRepository.GetUserRoleNamesAsync(user.Id, cancellationToken);
        var permissions = await _roleRepository.GetUserPermissionCodesAsync(user.Id, cancellationToken);
        var registeredVersion = 1;
        var registeredUser = await dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.Id == user.Id)
            .Select(x => x.SecurityVersion)
            .FirstOrDefaultAsync(cancellationToken);
        if (registeredUser > 0)
        {
            registeredVersion = registeredUser;
        }

        var token = _jwtTokenGenerator.GenerateToken(
            user.Id, user.Email, user.TenantId, roles, permissions, registeredVersion);

        _logger.LogInformation(
            "User {UserId} registered for Tenant {TenantId}.",
            user.Id,
            tenantId);

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

    private bool TryConsumeResetRequest(string normalizedEmail)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail.ToLowerInvariant())));
        var counter = _resetAttempts.GetOrCreate(
            "auth:password-reset:" + hash,
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return new ResetAttemptCounter();
            });

        if (counter is null)
        {
            return true;
        }

        return Interlocked.Increment(ref counter.Count) <= 5;
    }

    private static string SafeEmailError(Exception exception)
    {
        var message = exception.Message ?? exception.GetType().Name;
        var tokenIndex = message.IndexOf("token=", StringComparison.OrdinalIgnoreCase);
        if (tokenIndex >= 0)
        {
            message = message[..tokenIndex] + "token=[redacted]";
        }

        return message.Length > 300 ? message[..300] : message;
    }

    private async Task AssignRbacRoleAsync(string userId, string roleName, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetRoleByNameAsync(roleName, cancellationToken);
        if (role is not null)
        {
            await _roleRepository.AssignRoleToUserAsync(userId, role.Id, cancellationToken);
        }
    }

    private sealed class ResetAttemptCounter
    {
        public int Count;
    }
}
