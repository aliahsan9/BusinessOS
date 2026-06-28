using BusinessOS.Application.Features.Auth.DTOs;
using MediatR;

namespace BusinessOS.Application.Features.Auth.Commands.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string BusinessName)
    : IRequest<AuthResponse>;
