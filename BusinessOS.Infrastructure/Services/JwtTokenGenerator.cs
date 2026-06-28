using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BusinessOS.Application.Common.Authorization;
using BusinessOS.Application.Features.Auth.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BusinessOS.Infrastructure.Services;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(
        string userId,
        string email,
        Guid tenantId,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions)
    {
        var username = email.Split('@')[0];

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypesConstants.Username, username),
            new("TenantId", tenantId.ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (permissions.Count > 0)
        {
            claims.Add(new Claim(
                ClaimTypesConstants.Permissions,
                string.Join(',', permissions.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: GetTokenExpiration(),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public DateTime GetTokenExpiration() =>
        DateTime.UtcNow.AddMinutes(
            Convert.ToDouble(_configuration["Jwt:ExpiryMinutes"] ?? "60"));
}
