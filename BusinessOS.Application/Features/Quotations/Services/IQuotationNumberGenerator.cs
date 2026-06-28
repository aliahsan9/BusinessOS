namespace BusinessOS.Application.Features.Quotations.Services;

public interface IQuotationNumberGenerator
{
    Task<string> GenerateNextAsync(CancellationToken cancellationToken = default);
}
