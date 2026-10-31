using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Features.Auth.Services;

namespace BusinessOS.API.Security;

public static class AuthCookieWriter
{
    public const string RefreshCookieName = "bos_refresh";
    public const string CsrfCookieName = "bos_csrf";
    public const string CsrfHeaderName = "X-CSRF-Token";

    public static async Task IssueAsync(
        HttpContext http,
        IAuthSessionService sessions,
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var issued = await sessions.IssueAsync(
            userId,
            tenantId,
            http.Request.Headers.UserAgent.ToString(),
            cancellationToken);

        Write(http, issued);
    }

    public static void Write(HttpContext http, IssuedBrowserSession issued)
    {
        http.Response.Cookies.Append(RefreshCookieName, issued.RefreshToken, RefreshOptions(http, issued.RefreshExpiresAt));
        http.Response.Cookies.Append(CsrfCookieName, issued.CsrfToken, CsrfOptions(http, issued.RefreshExpiresAt));
        http.Response.Headers[CsrfHeaderName] = issued.CsrfToken;
    }

    public static void Clear(HttpContext http)
    {
        var expired = DateTimeOffset.UnixEpoch;
        http.Response.Cookies.Append(RefreshCookieName, string.Empty, RefreshOptions(http, expired));
        http.Response.Cookies.Append(CsrfCookieName, string.Empty, CsrfOptions(http, expired));
    }

    public static bool CsrfMatches(HttpRequest request)
    {
        var header = request.Headers[CsrfHeaderName].ToString();
        if (!request.Cookies.TryGetValue(CsrfCookieName, out var cookie))
        {
            return false;
        }

        if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(cookie))
        {
            return false;
        }

        var left = Encoding.UTF8.GetBytes(header);
        var right = Encoding.UTF8.GetBytes(cookie);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static CookieOptions RefreshOptions(HttpContext http, DateTimeOffset expires) =>
        new()
        {
            HttpOnly = true,
            Secure = UseSecure(http),
            SameSite = SameSiteMode.Lax,
            Path = "/api/v1/auth",
            Expires = expires,
            IsEssential = true
        };

    private static CookieOptions CsrfOptions(HttpContext http, DateTimeOffset expires) =>
        new()
        {
            HttpOnly = false,
            Secure = UseSecure(http),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expires,
            IsEssential = true
        };

    private static bool UseSecure(HttpContext http)
    {
        if (http.Request.IsHttps)
        {
            return true;
        }

        var environment = http.RequestServices.GetRequiredService<IHostEnvironment>();
        return !environment.IsDevelopment() && !environment.IsEnvironment("Testing");
    }
}
