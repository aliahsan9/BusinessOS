using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace BusinessOS.Infrastructure.Services;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
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
            TenantId = request.TenantId
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

    private static UserAuthResult MapUser(ApplicationUser user) =>
        new(user.Id, user.Email!, user.TenantId);
}
