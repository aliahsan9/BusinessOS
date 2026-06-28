using System.Net;
using System.Net.Http.Json;
using BusinessOS.Application.Features.Dashboard.DTOs;
using FluentAssertions;

namespace BusinessOS.IntegrationTests;

[Collection("IntegrationTests")]
public class DashboardIntegrationTests : IntegrationTestBase
{
    public DashboardIntegrationTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetDashboardOverview_AsAdmin_ReturnsKpis()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, auth, 15);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/dashboard/overview?period=all",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var overview = await response.Content.ReadFromJsonAsync<DashboardOverviewResponse>();
        overview.Should().NotBeNull();
        overview!.TotalProducts.Should().BeGreaterThan(0);
        overview.TotalActiveUsers.Should().BeGreaterThan(0);
        overview.DateRange.Period.Should().Be("all");
    }

    [Fact]
    public async Task GetSalesAnalytics_WithMonthPeriod_ReturnsSalesMetrics()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/dashboard/sales?period=month",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var sales = await response.Content.ReadFromJsonAsync<SalesAnalyticsResponse>();
        sales.Should().NotBeNull();
        sales!.DateRange.Period.Should().Be("month");
    }

    [Fact]
    public async Task GetProductAnalytics_WithTop20_ReturnsRanking()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, auth, 5);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/dashboard/products?period=all&top=20",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var products = await response.Content.ReadFromJsonAsync<ProductAnalyticsResponse>();
        products.Should().NotBeNull();
        products!.TopLimit.Should().Be(20);
    }

    [Fact]
    public async Task GetInventoryAnalytics_ReturnsStockInsights()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, auth, 2);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/dashboard/inventory?period=month",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var inventory = await response.Content.ReadFromJsonAsync<InventoryAnalyticsDashboardResponse>();
        inventory.Should().NotBeNull();
        inventory!.LowStockProducts.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetRevenueChart_ReturnsChartPayload()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/dashboard/charts/revenue?period=year",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var chart = await response.Content.ReadFromJsonAsync<ChartDataResponse>();
        chart.Should().NotBeNull();
        chart!.ChartType.Should().Be("line");
        chart.Datasets.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetDashboardOverview_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/api/dashboard/overview");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDashboardOverview_WithInvalidPeriod_ReturnsBadRequest()
    {
        var auth = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Get,
            "/api/dashboard/overview?period=invalid",
            auth);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
