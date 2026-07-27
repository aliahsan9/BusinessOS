using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Activities.DTOs;
using BusinessOS.Application.Features.Activities.Services;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Application.Features.Notifications.Services;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private const string ForgotPasswordSuccessMessage =
        "If an account exists for that email, password reset instructions have been sent.";

    private readonly IIdentityService _identityService;
    private readonly ITenantRegistrationService _tenantRegistrationService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ITenantProvider _tenantProvider;
    private readonly IDbContextFactory<BusinessOSDbContext> _dbContextFactory;
    private readonly IRoleRepository _roleRepository;
    private readonly IRbacAuditService _auditService;
    private readonly IActivityService _activityService;
    private readonly IEmailNotificationService _emailService;
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

        _tenantProvider.SetTenantId(user.TenantId);

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
                            "Login blocked for deactivated user {UserId} ({Email})",
                            user.Id,
                            email);
                        throw new UnauthorizedException("Account is deactivated.");
                    }

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
        var token = _jwtTokenGenerator.GenerateToken(user.Id, user.Email, user.TenantId, roles, permissions);

        _logger.LogInformation(
            "User {UserId} ({Email}) logged in successfully for Tenant {TenantId}. JWT generated.",
            user.Id,
            user.Email,
            user.TenantId);

        await _auditService.LogAsync(
            "UserLogin",
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

        _logger.LogInformation(
            "User {UserId} ({Email}) registered successfully for Tenant {TenantId}. JWT generated.",
            user.Id,
            user.Email,
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

    public async Task<PasswordResetResponse> ForgotPasswordAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim();
        var token = await _identityService.GeneratePasswordResetTokenAsync(normalizedEmail, cancellationToken);

        if (token is not null)
        {
            var baseUrl = _appOptions.FrontendBaseUrl.TrimEnd('/');
            var resetUrl =
                $"{baseUrl}/auth/reset-password" +
                $"?email={Uri.EscapeDataString(normalizedEmail)}" +
                $"&token={Uri.EscapeDataString(token)}";

            await _emailService.SendAsync(
                normalizedEmail,
                "Reset your BusinessOS password",
                BuildResetEmailBody(resetUrl),
                cancellationToken);

            _logger.LogInformation("Password reset email queued for {Email}", normalizedEmail);
        }
        else
        {
            _logger.LogInformation(
                "Password reset requested for unknown or inactive email {Email}",
                normalizedEmail);
        }

        return new PasswordResetResponse { Message = ForgotPasswordSuccessMessage };
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
            throw new BadRequestException(
                result.Errors.Count > 0
                    ? string.Join("; ", result.Errors)
                    : "Unable to reset password. The link may be invalid or expired.");
        }

        return new PasswordResetResponse { Message = "Your password has been reset successfully." };
    }

    private static string BuildResetEmailBody(string resetUrl) =>
        $"""
        You requested a password reset for your BusinessOS account.

        Open this link to choose a new password (valid for a limited time):
        {resetUrl}

        If you did not request this, you can ignore this email.
        """;

    private async Task AssignRbacRoleAsync(string userId, string roleName, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetRoleByNameAsync(roleName, cancellationToken);
        if (role is not null)
        {
            await _roleRepository.AssignRoleToUserAsync(userId, role.Id, cancellationToken);
        }
    }
}
