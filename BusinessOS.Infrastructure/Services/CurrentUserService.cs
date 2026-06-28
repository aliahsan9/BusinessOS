using System.Security.Claims;
using BusinessOS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BusinessOS.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId =>
        _httpContextAccessor.HttpContext?
            .User?
            .FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email =>
        _httpContextAccessor.HttpContext?
            .User?
            .FindFirstValue(ClaimTypes.Email);

    public Guid? TenantId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?
                .User?
                .FindFirst("TenantId")?
                .Value;

            return Guid.TryParse(value, out var tenantId)
                ? tenantId
                : null;
        }
    }
}
