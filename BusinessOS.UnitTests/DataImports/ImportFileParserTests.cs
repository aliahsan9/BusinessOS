using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Features.DataImports.Services;
using ClosedXML.Excel;

namespace BusinessOS.UnitTests.DataImports;

public sealed class ImportFileParserTests
{
    [Fact]
    public void Parse_Csv_ReadsHeadersAndRows()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("SKU,Name\r\nA1,Mouse\r\n");
        var result = ImportFileParser.Parse("products.csv", bytes);
        Assert.Equal("csv", result.Format);
        Assert.Equal(2, result.Headers.Count);
        Assert.Single(result.Rows);
        Assert.Equal("A1", result.Rows[0]["SKU"]);
        Assert.False(string.IsNullOrWhiteSpace(result.ContentHash));
    }

    [Fact]
    public void Parse_Xlsx_ReadsFirstWorksheet()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sheet1");
        sheet.Cell(1, 1).Value = "SKU";
        sheet.Cell(1, 2).Value = "Name";
        sheet.Cell(2, 1).Value = "SKU-1";
        sheet.Cell(2, 2).Value = "Cable";
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        var result = ImportFileParser.Parse("products.xlsx", ms.ToArray());
        Assert.Equal("xlsx", result.Format);
        Assert.Single(result.Rows);
        Assert.Equal("SKU-1", result.Rows[0]["SKU"]);
    }

    [Fact]
    public void Parse_RejectsEmptyFile()
    {
        Assert.Throws<BadRequestException>(() => ImportFileParser.Parse("empty.csv", []));
    }

    [Fact]
    public void Parse_RejectsLegacyXls()
    {
        Assert.Throws<BadRequestException>(() =>
            ImportFileParser.Parse("old.xls", System.Text.Encoding.UTF8.GetBytes("a,b")));
    }
}
