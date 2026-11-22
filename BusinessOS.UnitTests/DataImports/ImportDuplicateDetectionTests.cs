using BusinessOS.Application.Features.DataImports;
using BusinessOS.Application.Features.DataImports.Models;
using BusinessOS.Application.Features.DataImports.Services;
using BusinessOS.Domain.Entities;
using BusinessOS.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using BusinessOS.Application.Common.Interfaces;

namespace BusinessOS.UnitTests.DataImports;

public class ImportDuplicateDetectionTests
{
    [Fact]
    public async Task BuildPreview_FlagsDuplicateSkuInFile()
    {
        var tenantId = Guid.NewGuid();
        var products = TestMockDbSet.CreateMockDbSet(new List<Product>().AsQueryable());
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.Products).Returns(products.Object);

        var session = new DataImportPreviewSession
        {
            PreviewId = Guid.NewGuid(),
            TenantId = tenantId,
            EntityType = DataImportEntityTypes.Products,
            FileName = "products.csv",
            CsvColumns = ["SKU", "Name", "Category"],
            ColumnMapping = new Dictionary<string, string?>
            {
                ["SKU"] = "SKU",
                ["Name"] = "Name",
                ["CategoryName"] = "Category"
            },
            RawRows =
            [
                new Dictionary<string, string>
                {
                    ["SKU"] = "A-1",
                    ["Name"] = "Mouse",
                    ["Category"] = "Electronics"
                },
                new Dictionary<string, string>
                {
                    ["SKU"] = "A-1",
                    ["Name"] = "Mouse Duplicate",
                    ["Category"] = "Electronics"
                }
            ]
        };

        var preview = await ImportPreviewBuilder.BuildAsync(session, context.Object, CancellationToken.None);

        preview.PreviewRows.Should().ContainSingle(x => x.Errors.Any(e => e.Contains("Duplicate natural key")));
        preview.InvalidRows.Should().Be(1);
    }
}