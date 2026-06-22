namespace BusinessOS.Application.Features.Products.Queries;

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string SKU { get; set; } = default!;
    public decimal SalePrice { get; set; }
}
