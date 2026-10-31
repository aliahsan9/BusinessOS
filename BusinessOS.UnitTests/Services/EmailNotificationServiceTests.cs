using BusinessOS.Application.Common.Options;
using BusinessOS.Application.Features.Notifications.DTOs;
using BusinessOS.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessOS.UnitTests.Services;

public class EmailNotificationServiceTests
{
    [Fact]
    public async Task SendMessage_WhenDisabled_DoesNotLogTheMessageBody()
    {
        var logger = new CollectingLogger<EmailNotificationService>();
        var sut = new EmailNotificationService(
            Options.Create(new EmailOptions { Enabled = false, SmtpHost = "smtp.example.com" }),
            logger);

        var result = await sut.SendMessageAsync(
            new EmailMessage(
                "owner@acme.com",
                "Reset your BusinessOS password",
                "reset link token=super-secret-reset-token",
                "<a href=\"https://app.example.com/reset?token=super-secret-reset-token\">reset</a>"));

        result.Delivered.Should().BeFalse();
        result.ProviderConfigured.Should().BeFalse();
        logger.Text.Should().NotContain("super-secret-reset-token");
        logger.Text.Should().Contain("owner@acme.com");
    }

    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public string Text { get; private set; } = string.Empty;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Text += formatter(state, exception);
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose()
            {
            }
        }
    }
}
