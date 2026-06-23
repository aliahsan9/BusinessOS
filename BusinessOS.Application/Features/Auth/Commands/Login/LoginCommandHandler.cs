using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Application.Features.Auth.Services;
using BusinessOS.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace BusinessOS.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly UserManager<ApplicationUser>
        _userManager;

    private readonly IJwtTokenGenerator
        _jwtGenerator;

    public LoginCommandHandler(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtGenerator)
    {
        _userManager = userManager;
        _jwtGenerator = jwtGenerator;
    }

    public async Task<AuthResponse> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var user =
            await _userManager.FindByEmailAsync(
                request.Email);

        if (user is null)
            throw new Exception(
                "Invalid credentials");

        var passwordValid =
            await _userManager.CheckPasswordAsync(
                user,
                request.Password);

        if (!passwordValid)
            throw new Exception(
                "Invalid credentials");

        var roles =
            await _userManager.GetRolesAsync(user);

        var token =
            _jwtGenerator.GenerateToken(
                user,
                roles);

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
