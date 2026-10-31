using BusinessOS.Application.Common.Exceptions;

namespace BusinessOS.Infrastructure.Payments;

public static class BillingReturnUrls
{
    public static void Validate(string? url, IReadOnlyList<string>? allowedOrigins, bool allowLocalHttp)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new BadRequestException("Return URL must be an absolute URL.");

        var local = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host == "127.0.0.1";
        if (uri.Scheme != Uri.UriSchemeHttps && !(allowLocalHttp && local && uri.Scheme == Uri.UriSchemeHttp))
            throw new BadRequestException("Return URL must use HTTPS.");

        var origins = allowedOrigins ?? [];
        if (origins.Count == 0)
            throw new BadRequestException("No billing return origins are configured.");

        var origin = uri.GetLeftPart(UriPartial.Authority);
        var allowed = origins.Any(item =>
            string.Equals(item.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
            throw new BadRequestException("Return URL is not an allowed billing origin.");
    }
}
