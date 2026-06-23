namespace BusinessOS.Application.Features.Auth.DTOs;

public class RegisterRequest
{
    public string Email { get; set; } = default!;

    public string Password { get; set; } = default!;

    public string FirstName { get; set; } = default!;

    public string LastName { get; set; } = default!;

    public Guid TenantId { get; set; }
}
