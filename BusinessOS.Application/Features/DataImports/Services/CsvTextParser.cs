using System.Text;
using BusinessOS.Application.Common.Exceptions;

namespace BusinessOS.Application.Features.DataImports.Services;

/// <summary>
/// Lightweight RFC 4180-style CSV parser (no external dependencies).
/// </summary>
public static class CsvTextParser
{
    public const int MaxRows = ImportFileLimits.MaxDataRows;

    public static CsvParseResult Parse(string content, bool throwOnRowLimitExceeded = false)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new CsvParseResult([], []);

        var rows = new List<string[]>();
        var field = new StringBuilder();
        var current = new List<string>();
        var inQuotes = false;

        void EndField()
        {
            current.Add(field.ToString());
            field.Clear();
        }

        void EndRow()
        {
            EndField();
            if (current.Count > 0 && !(current.Count == 1 && string.IsNullOrWhiteSpace(current[0])))
                rows.Add(current.ToArray());
            current = [];
        }

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    EndField();
                    break;
                case '\r':
                    break;
                case '\n':
                    EndRow();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (field.Length > 0 || current.Count > 0)
            EndRow();

        if (rows.Count == 0)
            return new CsvParseResult([], []);

        var headers = rows[0].Select(h => h.Trim()).ToArray();
        var dataRowCount = rows.Count - 1;

        if (throwOnRowLimitExceeded && dataRowCount > MaxRows)
        {
            throw new BadRequestException(
                $"File contains {dataRowCount} data rows, which exceeds the maximum of {MaxRows}. " +
                "Split the file and import in smaller batches.");
        }

        var dataRows = rows.Skip(1).Take(MaxRows).ToList();

        var normalized = new List<IReadOnlyDictionary<string, string>>(dataRows.Count);
        foreach (var row in dataRows)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var col = 0; col < headers.Length; col++)
            {
                var key = headers[col];
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var value = col < row.Length ? row[col].Trim() : string.Empty;
                dict[key] = value;
            }

            if (dict.Values.All(string.IsNullOrWhiteSpace))
                continue;

            normalized.Add(dict);
        }

        return new CsvParseResult(headers, normalized);
    }
}

public sealed record CsvParseResult(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);
