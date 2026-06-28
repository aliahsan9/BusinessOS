using System.Reflection;
using BusinessOS.Application.Behaviors;
using BusinessOS.Application.Features.Dashboard.Services;
using BusinessOS.Application.Features.Inventory.Services;
using BusinessOS.Application.Features.Invoices.Services;
using BusinessOS.Application.Features.Orders.Services;
using BusinessOS.Application.Features.Quotations.Services;
using BusinessOS.Application.Features.Products.Commands.CreateProduct;
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
            typeof(CreateProductCommand).Assembly);

        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
        services.AddScoped<IQuotationNumberGenerator, QuotationNumberGenerator>();
        services.AddScoped<IInventoryService, InventoryService>();

        services.AddScoped<IDashboardDateRangeResolver, DashboardDateRangeResolver>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IReportingService, ReportingService>();
        services.AddScoped<IDashboardCacheService, DashboardCacheService>();

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(LoggingBehavior<,>));

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationBehavior<,>));

        return services;
    }
}
