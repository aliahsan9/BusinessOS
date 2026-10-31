using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessOS.Application.Features.Auth.DTOs;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.IntegrationTests;

[Collection("IntegrationTests")]
public class AuthSessionSecurityTests : IntegrationTestBase
{
    private readonly HttpClient _client;

    public AuthSessionSecurityTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    [Fact]
    public async Task Refresh_RotatesTheCookie_AndRejectsThePreviousToken()
    {
        var (auth, cookies) = await LoginAsync();
        auth.Token.Should().NotBeNullOrWhiteSpace();

        var refreshed = await RefreshAsync(cookies);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = ReadCookies(refreshed);
        var body = await refreshed.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        rotated["bos_refresh"].Should().NotBe(cookies["bos_refresh"]);

        var replay = await RefreshAsync(cookies);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var afterReplay = await RefreshAsync(rotated);
        afterReplay.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithoutCsrf_IsRejected()
    {
        var (_, cookies) = await LoginAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.TryAddWithoutValidation("Cookie", $"bos_refresh={cookies["bos_refresh"]}");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReplayedRefreshToken_RevokesTheFamily()
    {
        var (auth, cookies) = await LoginAsync();
        var refreshed = await RefreshAsync(cookies);
        refreshed.EnsureSuccessStatusCode();
        var rotated = ReadCookies(refreshed);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
            var previous = await db.AuthSessions.SingleAsync(x => x.UserId == auth.UserId && x.ReplacedBySessionId != null);
            previous.RevokedAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        var replay = await RefreshAsync(cookies);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var family = await RefreshAsync(rotated);
        family.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesOnlyTheCurrentSession()
    {
        var first = await LoginAsync();
        var second = await LoginAsync(first.Auth.Email);

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        ApplyCookies(logout, first.Cookies);
        (await _client.SendAsync(logout)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RefreshAsync(first.Cookies)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await RefreshAsync(second.Cookies)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LogoutAll_RevokesEverySession_AndTheCurrentAccessToken()
    {
        var first = await LoginAsync();
        var second = await LoginAsync(first.Auth.Email);

        var logoutAll = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout-all");
        logoutAll.Headers.Authorization = new AuthenticationHeaderValue("Bearer", first.Auth.Token);
        ApplyCookies(logoutAll, first.Cookies);
        (await _client.SendAsync(logoutAll)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RefreshAsync(second.Cookies)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var products = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products?page=1&pageSize=5");
        products.Headers.Authorization = new AuthenticationHeaderValue("Bearer", first.Auth.Token);
        var rejected = await _client.SendAsync(products);
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StaleAuthorizationVersion_IsRejected_UntilRefresh()
    {
        var (auth, cookies) = await LoginAsync();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == auth.UserId);
            user.SecurityVersion++;
            await db.SaveChangesAsync();
        }

        var stale = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products?page=1&pageSize=5");
        stale.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var rejected = await _client.SendAsync(stale);
        var body = await rejected.Content.ReadAsStringAsync();
        rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        body.Should().Contain("AUTH_REVOKED");

        var refreshed = await RefreshAsync(cookies);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var next = await refreshed.Content.ReadFromJsonAsync<AuthResponse>();

        var products = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products?page=1&pageSize=5");
        products.Headers.Authorization = new AuthenticationHeaderValue("Bearer", next!.Token);
        products.Headers.TryAddWithoutValidation("X-Tenant-ID", next.TenantId.ToString());
        (await _client.SendAsync(products)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeactivatedUser_CannotRefresh()
    {
        var (auth, cookies) = await LoginAsync();
        var deactivate = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/users/{auth.UserId}/deactivate",
            auth);
        deactivate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RefreshAsync(cookies)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LockedUser_CannotRefresh()
    {
        var (auth, cookies) = await LoginAsync();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(auth.UserId);
            await users.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddMinutes(10));
        }

        (await RefreshAsync(cookies)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredRefreshToken_IsRejected()
    {
        var (auth, cookies) = await LoginAsync();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BusinessOSDbContext>();
            var session = await db.AuthSessions.SingleAsync(x => x.UserId == auth.UserId && x.RevokedAt == null);
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        (await RefreshAsync(cookies)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConcurrentRefresh_AllowsOnlyOneWinner()
    {
        var (_, cookies) = await LoginAsync();
        var first = RefreshAsync(cookies);
        var second = RefreshAsync(cookies);
        var results = await Task.WhenAll(first, second);

        results.Count(x => x.StatusCode == HttpStatusCode.OK).Should().Be(1);
        results.Count(x => x.StatusCode == HttpStatusCode.Unauthorized).Should().Be(1);
    }

    private async Task<(AuthResponse Auth, Dictionary<string, string> Cookies)> LoginAsync(string? email = null)
    {
        HttpResponseMessage response;
        if (email is null)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
            {
                email = $"session_{suffix}@test.com",
                password = "Password1!",
                firstName = "Session",
                lastName = "User",
                businessName = $"Session {suffix}"
            });
        }
        else
        {
            response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email,
                password = "Password1!"
            });
        }

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("refreshToken");
        var auth = JsonSerializer.Deserialize<AuthResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
        return (auth, ReadCookies(response));
    }

    private async Task<HttpResponseMessage> RefreshAsync(Dictionary<string, string> cookies)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        ApplyCookies(request, cookies);
        return await _client.SendAsync(request);
    }

    private static Dictionary<string, string> ReadCookies(HttpResponseMessage response)
    {
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return cookies;
        }

        foreach (var value in values)
        {
            var pair = value.Split(';', 2)[0];
            var separator = pair.IndexOf('=');
            if (separator > 0)
            {
                cookies[pair[..separator]] = pair[(separator + 1)..];
            }
        }

        return cookies;
    }

    private static void ApplyCookies(HttpRequestMessage request, IReadOnlyDictionary<string, string> cookies)
    {
        request.Headers.TryAddWithoutValidation(
            "Cookie",
            string.Join("; ", cookies.Select(cookie => $"{cookie.Key}={cookie.Value}")));
        if (cookies.TryGetValue("bos_csrf", out var csrf))
        {
            request.Headers.TryAddWithoutValidation("X-CSRF-Token", csrf);
        }
    }
}
