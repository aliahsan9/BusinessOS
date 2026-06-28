namespace BusinessOS.Application.Features.SystemAdmin.DTOs;

public class SystemHealthResponse
{
    public string Status { get; set; } = default!;
    public bool DatabaseConnected { get; set; }
    public DateTime CheckedAt { get; set; }
    public IReadOnlyList<HealthCheckItem> Checks { get; set; } = [];
}

public class HealthCheckItem
{
    public string Name { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Message { get; set; }
}

public class SystemStatsResponse
{
    public int TotalTenants { get; set; }
    public int TotalUsers { get; set; }
    public int TotalProducts { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalOrders { get; set; }
    public int TotalInvoices { get; set; }
    public int TotalPayments { get; set; }
    public int TotalExpenses { get; set; }
    public int TotalNotifications { get; set; }
    public int TotalAuditLogs { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class EnvironmentInfoResponse
{
    public string Environment { get; set; } = default!;
    public string ApplicationName { get; set; } = default!;
    public string Version { get; set; } = default!;
    public string Framework { get; set; } = default!;
    public string MachineName { get; set; } = default!;
    public string OsVersion { get; set; } = default!;
    public DateTime ServerTimeUtc { get; set; }
}
