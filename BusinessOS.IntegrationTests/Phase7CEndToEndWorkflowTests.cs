using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessOS.Application.Features.Auth.DTOs;
using FluentAssertions;

namespace BusinessOS.IntegrationTests;

/// <summary>
/// Phase 7C end-to-end HTTP workflows against an isolated InMemory test database.
/// These tests do not touch production data.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "E2E")]
[Trait("Phase", "7C")]
public class Phase7CEndToEndWorkflowTests : IntegrationTestBase
{
    public Phase7CEndToEndWorkflowTests(BusinessOSWebApplicationFactory factory)
        : base(factory)
    {
    }

    /// <summary>
    /// Workflow A: product → inventory → customer → sale → confirm → inventory deduction → finance records.
    /// </summary>
    [Fact]
    public async Task WorkflowA_ProductInventorySale_ConfirmsOrder_DeductsStock_AndCreatesFinanceRecords()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        const decimal initialStock = 20m;
        const decimal saleQty = 3m;

        var categoryId = await CreateCategoryAsync(owner, $"Phase7C Cat {Guid.NewGuid():N}");
        var productId = await CreateProductAsync(owner, categoryId, cost: 10m, sale: 40m, reorder: 2m);
        await IntegrationInventoryHelper.IncreaseStockAsync(Client, owner, productId, initialStock);

        var stockBefore = await GetInventoryStockAsync(owner, productId);
        stockBefore.Should().Be(initialStock);

        var customerId = await CreateCustomerAsync(owner);

        var orderResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/orders",
            owner,
            new
            {
                customerId,
                discount = 0,
                tax = 0,
                items = new[] { new { productId, quantity = saleQty } }
            });
        orderResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var orderBody = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = orderBody.GetProperty("id").GetGuid();

        foreach (var status in new[] { "Confirmed", "Processing", "Completed" })
        {
            var advance = await IntegrationHttp.SendAuthorizedAsync(
                Client,
                HttpMethod.Patch,
                $"/api/v1/orders/{orderId}/status",
                owner,
                new { status });
            advance.StatusCode.Should().Be(HttpStatusCode.NoContent, $"expected transition to {status}");
        }

        var orderGet = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/orders/{orderId}", owner);
        orderGet.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = await orderGet.Content.ReadFromJsonAsync<JsonElement>();
        order.GetProperty("status").GetString().Should().Be("Completed");
        order.GetProperty("grandTotal").GetDecimal().Should().Be(saleQty * 40m);

        var stockAfter = await GetInventoryStockAsync(owner, productId);
        stockAfter.Should().Be(initialStock - saleQty);

        var invoiceResponse = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            $"/api/v1/invoices/from-order/{orderId}",
            owner,
            new { dueDate = DateTime.UtcNow.AddDays(14), notes = "Phase7C Workflow A" });
        invoiceResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var invoice = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = invoice.GetProperty("id").GetGuid();
        invoiceId.Should().NotBeEmpty();

        var invoiceGet = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/invoices/{invoiceId}", owner);
        invoiceGet.StatusCode.Should().Be(HttpStatusCode.OK);
        var invoiceDetail = await invoiceGet.Content.ReadFromJsonAsync<JsonElement>();
        invoiceDetail.GetProperty("orderId").GetGuid().Should().Be(orderId);
        invoiceDetail.GetProperty("grandTotal").GetDecimal().Should().BeGreaterThan(0);

        var finance = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, "/api/v1/finance/dashboard", owner);
        finance.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await finance.Content.ReadFromJsonAsync<JsonElement>();
        dashboard.ValueKind.Should().Be(JsonValueKind.Object);
    }

    /// <summary>
    /// Failure path for Workflow A: sale that would oversell must leave stock unchanged.
    /// </summary>
    [Fact]
    public async Task WorkflowA_Oversell_IsRejected_AndStockUnchanged()
    {
        var owner = await IntegrationHttp.RegisterAndAuthenticateAsync(Client);
        var productId = await IntegrationInventoryHelper.CreateProductWithStockAsync(Client, owner, 2);
        var customerId = await CreateCustomerAsync(owner);

        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/orders",
            owner,
            new
            {
                customerId,
                discount = 0,
                tax = 0,
                items = new[] { new { productId, quantity = 5m } }
            });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GetInventoryStockAsync(owner, productId)).Should().Be(2);
    }

    private async Task<Guid> CreateCategoryAsync(AuthResponse owner, string name)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Post, "/api/v1/categories", owner, new { name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateProductAsync(
        AuthResponse owner,
        Guid categoryId,
        decimal cost,
        decimal sale,
        decimal reorder)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/products",
            owner,
            new
            {
                categoryId,
                name = $"Phase7C Product {Guid.NewGuid():N}"[..40],
                sku = $"P7C-{Guid.NewGuid():N}"[..20],
                costPrice = cost,
                salePrice = sale,
                reorderLevel = reorder
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateCustomerAsync(AuthResponse owner)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client,
            HttpMethod.Post,
            "/api/v1/customers",
            owner,
            new
            {
                firstName = "Phase7C",
                lastName = "Buyer",
                email = $"buyer_{Guid.NewGuid():N}@test.com",
                phoneNumber = "555-0700",
                address = "1 Workflow Street",
                city = "Lahore",
                country = "Pakistan",
                postalCode = "54000"
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<decimal> GetInventoryStockAsync(AuthResponse owner, Guid productId)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            Client, HttpMethod.Get, $"/api/v1/inventory/{productId}", owner);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("currentStock").GetDecimal();
    }
}
