using BusinessOS.Application.Features.DataImports;
using BusinessOS.Application.Features.DataImports.Services;
using FluentAssertions;

namespace BusinessOS.UnitTests.DataImports;

public class ImportColumnMapperTests
{
    [Fact]
    public void MapColumns_MapsProductAliases()
    {
        var headers = new[] { "Product SKU", "Title", "Category Name", "Selling Price" };

        var mapping = ImportColumnMapper.MapColumns(headers, DataImportEntityTypes.Products);

        mapping["SKU"].Should().Be("Product SKU");
        mapping["Name"].Should().Be("Title");
        mapping["CategoryName"].Should().Be("Category Name");
        mapping["SalePrice"].Should().Be("Selling Price");
    }

    [Fact]
    public void ResolveNaturalKey_DetectsSupplierEmailOverName()
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Name"] = "Acme",
            ["Email"] = "ops@acme.com"
        };

        var mapping = new Dictionary<string, string?>
        {
            ["Name"] = "Name",
            ["Email"] = "Email"
        };

        var key = ImportPreviewBuilder.ResolveNaturalKey(
            DataImportEntityTypes.Suppliers,
            row,
            mapping);

        key.Should().Be("email:ops@acme.com");
    }
}