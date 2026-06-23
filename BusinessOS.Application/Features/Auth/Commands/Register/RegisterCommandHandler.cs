using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace BusinessOS.Application.Features.Auth.Commands.Register;

public sealed class RegisterCommandHandler
    : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IJwtTokenGenerator
        _jwtGenerator;

    public RegisterCommandHandler(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtGenerator)
    {
        _userManager = userManager;
        _jwtGenerator = jwtGenerator;
    }

    public async Task<AuthResponse> Handle(
        RegisterCommand request,
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

        var result =
            await _userManager.CreateAsync(
                user,
                request.Password);

        if (!result.Succeeded)
        {
            throw new Exception(
                string.Join(",",
                    result.Errors.Select(x =>
                        x.Description)));
        }

        await _userManager.AddToRoleAsync(
            user,
            "Admin");

        var token =
            _jwtGenerator.GenerateToken(
                user,
                new List<string> { "Admin" });

        return new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email!,
            ExpiresAt =
                DateTime.UtcNow.AddHours(1)
        };
    }
}
