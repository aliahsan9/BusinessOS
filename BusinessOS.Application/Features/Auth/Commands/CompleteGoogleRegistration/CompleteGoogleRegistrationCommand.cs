using BusinessOS.Application.Features.Auth.DTOs;
using MediatR;

namespace BusinessOS.Application.Features.Auth.Commands.CompleteGoogleRegistration;

public sealed record CompleteGoogleRegistrationCommand(
    string ContinuationToken,
    string BusinessName,
    string FirstName,
    string LastName) : IRequest<AuthResponse>;
