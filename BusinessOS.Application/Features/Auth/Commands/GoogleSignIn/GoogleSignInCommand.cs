using BusinessOS.Application.Features.Auth.DTOs;
using MediatR;

namespace BusinessOS.Application.Features.Auth.Commands.GoogleSignIn;

public sealed record GoogleSignInCommand(string IdToken) : IRequest<GoogleSignInResponse>;
