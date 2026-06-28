using System.Reflection;
using BusinessOS.Application.Common.Interfaces;
using BusinessOS.Application.Features.SystemAdmin.DTOs;
using BusinessOS.Application.Features.SystemAdmin.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BusinessOS.Infrastructure.Services;

public sealed class SystemAdminService : ISystemAdminService
{
    private readonly IApplicationDbContext _context;
    private readonly IHostEnvironment _environment;

    public SystemAdminService(
        IApplicationDbContext context,
        IHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
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
            checks.Add(new HealthCheckItem
            {
                Name = "Database",
                Status = "Unhealthy",
                Message = ex.Message
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
        return new SystemStatsResponse
        {
            TotalTenants = await _context.Tenants.CountAsync(cancellationToken),
            TotalUsers = await _context.RbacUserRoles
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync(cancellationToken),
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
            MachineName = Environment.MachineName,
            OsVersion = Environment.OSVersion.VersionString,
            ServerTimeUtc = DateTime.UtcNow
        });
    }
}
