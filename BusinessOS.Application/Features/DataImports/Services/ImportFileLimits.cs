namespace BusinessOS.Application.Features.DataImports.Services;

public static class ImportFileLimits
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxDataRows = 5000;
    public const string MaxFileSizeLabel = "5 MB";
}
