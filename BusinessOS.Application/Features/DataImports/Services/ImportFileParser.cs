using System.Security.Cryptography;
using System.Text;
using BusinessOS.Application.Common.Exceptions;
using ClosedXML.Excel;

namespace BusinessOS.Application.Features.DataImports.Services;

public sealed record ImportParseResult(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows,
    string ContentHash,
    string Format);

public static class ImportFileParser
{
    public static ImportParseResult Parse(string fileName, byte[] content)
    {
        if (content.Length == 0)
            throw new BadRequestException("Uploaded file is empty.");

        if (content.Length > ImportFileLimits.MaxFileBytes)
        {
            throw new BadRequestException(
                $"File exceeds the maximum size of {ImportFileLimits.MaxFileSizeLabel}. " +
                "Split the file into smaller batches and try again.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".csv" or ".txt" => ParseCsv(content, hash),
            ".xlsx" => ParseXlsx(content, hash),
            ".xls" => throw new BadRequestException(
                "Legacy Excel (.xls) files are not supported. Save the workbook as .xlsx or export CSV."),
            _ => throw new BadRequestException(
                "Unsupported file type. Upload a .csv or .xlsx file.")
        };
    }

    private static ImportParseResult ParseCsv(byte[] content, string hash)
    {
        var text = Encoding.UTF8.GetString(content);
        // Strip UTF-8 BOM if present
        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text[1..];

        var parsed = CsvTextParser.Parse(text, throwOnRowLimitExceeded: true);
        return new ImportParseResult(parsed.Headers, parsed.Rows, hash, "csv");
    }

    private static ImportParseResult ParseXlsx(byte[] content, string hash)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new BadRequestException("Excel workbook has no worksheets.");

        var used = worksheet.RangeUsed();
        if (used is null)
            throw new BadRequestException("Excel worksheet is empty.");

        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();

        var headers = new List<string>();
        for (var col = firstCol; col <= lastCol; col++)
        {
            var header = worksheet.Cell(firstRow, col).GetString().Trim();
            headers.Add(header);
        }

        if (headers.All(string.IsNullOrWhiteSpace))
            throw new BadRequestException("Excel file has no header row.");

        var dataRowCount = lastRow - firstRow;
        if (dataRowCount > ImportFileLimits.MaxDataRows)
        {
            throw new BadRequestException(
                $"File contains {dataRowCount} data rows, which exceeds the maximum of {ImportFileLimits.MaxDataRows}. " +
                "Split the file and import in smaller batches.");
        }

        var rows = new List<IReadOnlyDictionary<string, string>>();
        for (var row = firstRow + 1; row <= lastRow; row++)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var col = firstCol; col <= lastCol; col++)
            {
                var headerIndex = col - firstCol;
                var key = headers[headerIndex];
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var cell = worksheet.Cell(row, col);
                dict[key] = GetCellString(cell);
            }

            if (dict.Values.All(string.IsNullOrWhiteSpace))
                continue;

            rows.Add(dict);
        }

        if (rows.Count == 0)
            throw new BadRequestException("Excel file contains no data rows.");

        return new ImportParseResult(headers, rows, hash, "xlsx");
    }

    private static string GetCellString(IXLCell cell)
    {
        if (cell.IsEmpty())
            return string.Empty;

        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().ToString("yyyy-MM-dd");

        if (cell.DataType == XLDataType.Number)
            return cell.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (cell.DataType == XLDataType.Boolean)
            return cell.GetBoolean() ? "true" : "false";

        return cell.GetString().Trim();
    }
}
