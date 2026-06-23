using BusinessOS.Application.Features.Auth.DTOs;
using MediatR;

namespace BusinessOS.Application.Features.Auth.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password)
    : IRequest<AuthResponse>;
