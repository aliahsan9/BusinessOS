namespace BusinessOS.Application.Common.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// When false (default), emails are logged instead of sent.
    /// </summary>
    public bool Enabled { get; set; }

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "aliahsan8751@gmail.com";

    public string FromName { get; set; } = "BusinessOS";
}
