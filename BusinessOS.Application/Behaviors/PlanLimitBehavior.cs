using BusinessOS.Application.Features.Tenant.Services;
using MediatR;

namespace BusinessOS.Application.Behaviors;

public sealed class PlanLimitBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ITenantLimitService _limits;

    public PlanLimitBehavior(ITenantLimitService limits) => _limits = limits;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var resource = typeof(TRequest).Name switch
        {
            "CreateProductCommand" => "products",
            "CreateProjectCommand" => "projects",
            "CreateWorkTaskCommand" => "tasks",
            _ => null
        };

        if (resource is not null)
            await _limits.EnsureWithinLimitAsync(resource, cancellationToken);

        return await next();
    }
}
