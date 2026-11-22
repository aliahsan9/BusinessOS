namespace BusinessOS.Application.Features.DataImports.Services;

public static class ImportColumnMapper
{
    public static Dictionary<string, string?> MapColumns(
        IReadOnlyList<string> csvHeaders,
        string entityType)
    {
        var fields = ImportEntityDefinitions.GetFields(entityType);
        var mapping = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
            mapping[field.Key] = null;

        var normalizedHeaders = csvHeaders
            .Select(h => new { Original = h, Normalized = NormalizeHeader(h) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Normalized))
            .ToList();

        foreach (var field in fields)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizeHeader(field.Label),
                NormalizeHeader(field.Key)
            };

            foreach (var alias in field.Aliases)
                candidates.Add(NormalizeHeader(alias));

            var match = normalizedHeaders.FirstOrDefault(h => candidates.Contains(h.Normalized));
            if (match is not null)
                mapping[field.Key] = match.Original;
        }

        return mapping;
    }

    public static string GetMappedValue(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string?> mapping,
        string fieldKey)
    {
        if (!mapping.TryGetValue(fieldKey, out var column) || string.IsNullOrWhiteSpace(column))
            return string.Empty;

        return row.TryGetValue(column, out var value) ? value.Trim() : string.Empty;
    }

    public static string NormalizeHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return string.Empty;

        var chars = header.Trim().ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray();

        return new string(chars);
    }
}