namespace BusinessOS.Application.Features.Invoices.Services;

public interface IInvoiceNumberGenerator
{
    Task<string> GenerateNextAsync(CancellationToken cancellationToken = default);
}
