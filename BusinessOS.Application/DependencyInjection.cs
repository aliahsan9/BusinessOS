using System.Reflection;
using BusinessOS.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace BusinessOS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(
                Assembly.GetExecutingAssembly());
        });

        services.AddValidatorsFromAssembly(
            Assembly.GetExecutingAssembly());
        services.AddTransient(
    typeof(IPipelineBehavior<,>),
    typeof(ValidationBehavior<,>));

        return services;
    }
}
