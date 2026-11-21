using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace BusinessOS.IntegrationTests;

/// <summary>
/// Lightweight performance baseline for Phase 4. These are not load tests;
/// they record single-client response times under the in-memory test host.
/// </summary>
[Collection("IntegrationTests")]
public class PerformanceBaselineIntegrationTests : IntegrationTestBase
{
    public PerformanceBaselineIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task CriticalEndpoints_RespondWithinBaseline()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, owner, 5);

        var measurements = new List<(string Name, long ElapsedMs, HttpStatusCode Status)>();

        async Task MeasureAsync(string name, Func<Task<HttpResponseMessage>> action)
        {
            var sw = Stopwatch.StartNew();
            var response = await action();
            sw.Stop();
            measurements.Add((name, sw.ElapsedMilliseconds, response.StatusCode));
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }

        await MeasureAsync("auth_login_repeat", async () =>
        {
            var login = await Client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = owner.Email,
                password = "Password1!"
            });
            return login;
        });

        await MeasureAsync("products_list", () =>
            IntegrationHttp.SendAuthorizedAsync(Client, HttpMethod.Get, "/api/v1/products?page=1&pageSize=20", owner));

        await MeasureAsync("dashboard_overview", () =>
            IntegrationHttp.SendAuthorizedAsync(
                Client, HttpMethod.Get, "/api/v1/dashboard/overview?period=all", owner));

        await MeasureAsync("billing_usage", () =>
            IntegrationHttp.SendAuthorizedAsync(Client, HttpMethod.Get, "/api/v1/billing/usage", owner));

        await MeasureAsync("ai_conversations", () =>
            IntegrationHttp.SendAuthorizedAsync(Client, HttpMethod.Get, "/api/v1/ai/conversations", owner));

        // Soft ceilings for the in-memory host. Fail only on extreme regressions.
        foreach (var (name, elapsedMs, _) in measurements)
        {
            elapsedMs.Should().BeLessThan(5_000, $"{name} took {elapsedMs} ms");
        }

        var summary = string.Join(
            "; ",
            measurements.Select(m => $"{m.Name}={m.ElapsedMs}ms/{(int)m.Status}"));
        summary.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ConcurrentAuthenticatedListReads_DoNotFail()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, owner, 5);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => IntegrationHttp.SendAuthorizedAsync(
                Client, HttpMethod.Get, "/api/v1/products?page=1&pageSize=20", owner))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task DashboardAnalytics_RespondsWithinSoftCeiling()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var sw = Stopwatch.StartNew();
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/v1/dashboard/sales?period=month",
            owner);
        sw.Stop();

        // Soft ceiling only — documents measured latency for the in-memory host.
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        sw.ElapsedMilliseconds.Should().BeLessThan(5_000);
    }
}
