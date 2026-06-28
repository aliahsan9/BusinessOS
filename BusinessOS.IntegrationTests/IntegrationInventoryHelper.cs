using System.Net.Http.Json;
using System.Text.Json;
using BusinessOS.Application.Features.Auth.DTOs;

namespace BusinessOS.IntegrationTests;

internal static class IntegrationInventoryHelper
{
    public static async Task IncreaseStockAsync(
        HttpClient client,
        AuthResponse auth,
        Guid productId,
        decimal quantity = 100)
    {
        var response = await IntegrationHttp.SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/inventory/increase",
            auth,
            new
            {
                productId,
                quantity,
                notes = "Test stock seed"
            });

        response.EnsureSuccessStatusCode();
    }

    public static async Task<Guid> CreateProductWithStockAsync(
        HttpClient client,
        AuthResponse auth,
        decimal initialStock = 100)
    {
        var categoryResponse = await IntegrationHttp.SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/categories",
            auth,
            new { name = $"Cat {Guid.NewGuid():N}" });

        categoryResponse.EnsureSuccessStatusCode();
        var category = await categoryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var categoryId = category.GetProperty("id").GetGuid();

        var productResponse = await IntegrationHttp.SendAuthorizedAsync(
            client,
            HttpMethod.Post,
            "/api/products",
            auth,
            new
            {
                categoryId,
                name = "Inventory Product",
                sku = $"SKU-{Guid.NewGuid():N}",
                costPrice = 10,
                salePrice = 25,
                reorderLevel = 5
            });

        productResponse.EnsureSuccessStatusCode();
        var product = await productResponse.Content.ReadFromJsonAsync<JsonElement>();
        var productId = product.GetProperty("id").GetGuid();

        if (initialStock > 0)
            await IncreaseStockAsync(client, auth, productId, initialStock);

        return productId;
    }
}
