using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;

namespace BusinessOS.IntegrationTests;

[Collection("IntegrationTests")]
public class PaymentAndSignalRSecurityTests : IntegrationTestBase
{
    public PaymentAndSignalRSecurityTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task JazzCashCallback_WithoutSignature_IsRejected()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/billing/webhook/jazzcash", new
        {
            pp_ResponseCode = "000",
            pp_TxnRefNo = "JC-forged"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EasyPaisaCallback_ClaimingSuccess_IsRejected()
    {
        var response = await Client.PostAsJsonAsync("/api/v1/billing/webhook/easypaisa", new
        {
            status = "success",
            orderId = "EP-forged"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccessTokenQueryString_IsIgnoredOutsideTheNotificationHub()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/products?page=1&pageSize=5&access_token={auth.Token}");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NotificationHubNegotiate_RequiresAuthentication()
    {
        var anonymous = await Client.PostAsync("/hubs/notifications/negotiate?negotiateVersion=1", null);
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/hubs/notifications/negotiate?negotiateVersion=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var authenticated = await Client.SendAsync(request);
        authenticated.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}
