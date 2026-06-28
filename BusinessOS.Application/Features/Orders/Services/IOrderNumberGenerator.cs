namespace BusinessOS.Application.Features.Orders.Services;

public interface IOrderNumberGenerator
{
    Task<string> GenerateNextAsync(CancellationToken cancellationToken = default);
}
