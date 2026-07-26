namespace BusinessOS.Application.Features.VectorSearch.Options;

public sealed class VectorSyncOptions
{
    public const string SectionName = "VectorSync";

    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 25;
    public int MaxAttempts { get; set; } = 8;
    public bool BackfillOnStartup { get; set; } = true;
}
