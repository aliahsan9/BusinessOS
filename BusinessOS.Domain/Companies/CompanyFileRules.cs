namespace BusinessOS.Domain.Companies;

public static class CompanyFileRules
{
    public const long MaxBytes = 10 * 1024 * 1024;

    public static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".txt", ".csv", ".docx"
    };

    public static void Validate(string fileName, long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new InvalidOperationException("A file name is required.");

        if (fileName.IndexOfAny(['/', '\\', '\0']) >= 0)
            throw new InvalidOperationException("The file name is not allowed.");

        if (sizeBytes <= 0)
            throw new InvalidOperationException("The file is empty.");

        if (sizeBytes > MaxBytes)
            throw new InvalidOperationException("Files must be 10 MB or smaller.");

        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException("This file type is not allowed.");
    }
}
