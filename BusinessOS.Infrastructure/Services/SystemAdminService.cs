using System.Reflection;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.SystemAdmin.DTOs;
using BusinessOS.Application.Features.SystemAdmin.Services;
using BusinessOS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BusinessOS.Infrastructure.Services;

public sealed class SystemAdminService : ISystemAdminService
{
    private readonly IApplicationDbContext _context;
    private readonly IHostEnvironment _environment;
    private readonly ITenantProvider _tenantProvider;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SystemAdminService> _logger;

    public SystemAdminService(
        IApplicationDbContext context,
        IHostEnvironment environment,
        ITenantProvider tenantProvider,
        UserManager<ApplicationUser> userManager,
        ILogger<SystemAdminService> logger)
    {
        _context = context;
        _environment = environment;
        _tenantProvider = tenantProvider;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<SystemHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var checks = new List<HealthCheckItem>();
        var databaseConnected = false;

        try
        {
            databaseConnected = await _context.Tenants.AnyAsync(cancellationToken);
            checks.Add(new HealthCheckItem
            {
                Name = "Database",
                Status = "Healthy",
                Message = "Database connection is available."
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "System health database check failed for tenant {TenantId}", _tenantProvider.TenantId);
            checks.Add(new HealthCheckItem
            {
                Name = "Database",
                Status = "Unhealthy",
                Message = "Database connection failed."
            });
        }

        var status = databaseConnected ? "Healthy" : "Unhealthy";

        return new SystemHealthResponse
        {
            Status = status,
            DatabaseConnected = databaseConnected,
            CheckedAt = DateTime.UtcNow,
            Checks = checks
        };
    }

    public async Task<SystemStatsResponse> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.TenantId;
        var totalUsers = tenantId == Guid.Empty
            ? 0
            : await _userManager.Users.CountAsync(user => user.TenantId == tenantId, cancellationToken);

        return new SystemStatsResponse
        {
            TotalTenants = await _context.Tenants.CountAsync(cancellationToken),
            TotalUsers = totalUsers,
            TotalProducts = await _context.Products.CountAsync(cancellationToken),
            TotalCustomers = await _context.Customers.CountAsync(cancellationToken),
            TotalOrders = await _context.Orders.CountAsync(cancellationToken),
            TotalInvoices = await _context.Invoices.CountAsync(cancellationToken),
            TotalPayments = await _context.Payments.CountAsync(cancellationToken),
            TotalExpenses = await _context.Expenses.CountAsync(cancellationToken),
            TotalNotifications = await _context.Notifications.CountAsync(cancellationToken),
            TotalAuditLogs = await _context.RbacAuditLogs.CountAsync(cancellationToken),
            GeneratedAt = DateTime.UtcNow
        };
    }

    public Task<EnvironmentInfoResponse> GetEnvironmentInfoAsync(CancellationToken cancellationToken = default)
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "1.0.0";

        return Task.FromResult(new EnvironmentInfoResponse
        {
            Environment = _environment.EnvironmentName,
            ApplicationName = assembly.GetName().Name ?? "BusinessOS.API",
            Version = version,
            Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            MachineName = string.Empty,
            OsVersion = string.Empty,
            ServerTimeUtc = DateTime.UtcNow
        });
    }
}
