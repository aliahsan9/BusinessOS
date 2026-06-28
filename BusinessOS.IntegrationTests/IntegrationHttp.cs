using System.Net.Http.Headers;
using System.Net.Http.Json;
using BusinessOS.Application.Features.Auth.DTOs;

namespace BusinessOS.IntegrationTests;

internal static class IntegrationHttp
{
    public static async Task<AuthResponse> RegisterAndAuthenticateAsync(HttpClient client)
    {
        var email = $"user_{Guid.NewGuid():N}@test.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            firstName = "Test",
            lastName = "User",
            businessName = "Test Business"
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        AuthResponse auth,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        request.Headers.TryAddWithoutValidation("X-Tenant-ID", auth.TenantId.ToString());

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }
}
