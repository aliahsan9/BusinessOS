using System.Collections.Concurrent;
using System.Data;
using System.Text;
using BusinessOS.Application.Common.Exceptions;
using BusinessOS.Application.Common.Models;
using BusinessOS.Application.Features.Billing.Services;
using BusinessOS.Domain.Billing;
using BusinessOS.Domain.Entities;
using BusinessOS.Domain.Enums;
using BusinessOS.Infrastructure.Data;
using BusinessOS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace BusinessOS.Infrastructure.Billing;

/// <summary>
/// Serializes quota decisions per tenant. SQL Server uses an exclusive application lock
/// held for the database transaction. The in-memory provider uses a process lock so tests
/// can exercise the same check-and-insert critical section.
/// </summary>
internal static class QuotaEnforcer
{
    private static readonly AsyncLocal<int> Depth = new();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ProcessLocks = new();

    public static bool IsReentrant => Depth.Value > 0;

    public static async Task<QuotaHold> AcquireForSaveAsync(BusinessOSDbContext db, CancellationToken cancellationToken)
    {
        if (Depth.Value > 0)
            return QuotaHold.Noop;

        var demand = QuotaDemand.Collect(db);
        if (!demand.HasPositiveDemand())
            return QuotaHold.Noop;

        return await LockAndAssertAsync(db, demand, cancellationToken);
    }

    public static async Task<QuotaHold> LockTenantAsync(
        BusinessOSDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (Depth.Value > 0 || tenantId == Guid.Empty)
            return QuotaHold.Noop;

        var hold = new QuotaHold { OwnsDepth = true };
        Depth.Value++;
        try
        {
            await AcquireLocksAsync(db, hold, [tenantId], cancellationToken);
            return hold;
        }
        catch
        {
            hold.Success = false;
            await hold.DisposeAsync();
            throw;
        }
    }

    public static async Task<ResourceUsage> MeasureAsync(
        BusinessOSDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var activeUsers = await db.Users
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

        var pendingInvitations = await db.TeamInvitations
            .IgnoreQueryFilters()
            .CountAsync(
                x => x.TenantId == tenantId
                    && !x.IsDeleted
                    && x.Status == InvitationStatus.Pending
                    && x.ExpiresAt > now,
                cancellationToken);

        var customers = await db.Customers
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

        var products = await db.Products
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

        var projects = await db.Projects
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

        var tasks = await db.WorkTasks
            .IgnoreQueryFilters()
            .CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

        var reportBytes = await db.GeneratedReports
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.FileContent != null)
            .Select(x => (int?)x.FileContent!.Length)
            .SumAsync(cancellationToken) ?? 0;

        var documentContents = await db.AiDocuments
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Content != null)
            .Select(x => x.Content)
            .ToListAsync(cancellationToken);
        var documentBytes = documentContents.Sum(content => (long)Encoding.UTF8.GetByteCount(content));

        var usage = await db.TenantUsages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted, cancellationToken);

        return new ResourceUsage(
            activeUsers,
            pendingInvitations,
            customers,
            products,
            projects,
            tasks,
            reportBytes + documentBytes,
            usage?.AiRequestsUsed ?? 0,
            usage?.AiPeriodStart);
    }

    public static async Task<TenantLimits?> TryGetLimitsAsync(
        BusinessOSDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Plan)
            .Include(x => x.Subscription)
            .FirstOrDefaultAsync(x => x.Id == tenantId && !x.IsDeleted, cancellationToken);

        if (tenant?.Plan is null)
            return null;

        return EntitlementResolver.Resolve(tenant.Plan, tenant.Subscription);
    }

    public static void ThrowIfExceeded(QuotaResource resource, long current, long additional, long maximum)
    {
        if (!Exceeds(current, additional, maximum))
            return;

        var (reportedCurrent, reportedMax) = resource == QuotaResource.Storage
            ? (ToDisplayMb(current), maximum)
            : (current, maximum);

        throw new PlanLimitExceededException(ResourceName(resource), reportedCurrent, reportedMax);
    }

    public static bool Exceeds(long current, long additional, long maximum)
    {
        if (additional <= 0 || maximum < 0)
            return false;

        return current > long.MaxValue - additional || current + additional > maximum;
    }

    public static long LimitFor(TenantLimits limits, QuotaResource resource) => resource switch
    {
        QuotaResource.Users => limits.MaxUsers,
        QuotaResource.Customers => limits.MaxCustomers,
        QuotaResource.Products => limits.MaxProducts,
        QuotaResource.Projects => limits.MaxProjects,
        QuotaResource.Tasks => limits.MaxTasks,
        QuotaResource.AiRequests => limits.MaxAiRequests,
        QuotaResource.Storage => limits.MaxStorageMb,
        _ => 0
    };

    public static long CurrentFor(ResourceUsage usage, QuotaResource resource) => resource switch
    {
        QuotaResource.Users => usage.OccupiedSeats,
        QuotaResource.Customers => usage.Customers,
        QuotaResource.Products => usage.Products,
        QuotaResource.Projects => usage.Projects,
        QuotaResource.Tasks => usage.Tasks,
        QuotaResource.AiRequests => usage.AiRequestsUsed,
        QuotaResource.Storage => usage.StorageBytes,
        _ => 0
    };

    public static long ToDisplayMb(long bytes) =>
        bytes <= 0 ? 0 : (bytes + (1024L * 1024L) - 1) / (1024L * 1024L);

    public static long MegabytesToBytes(long megabytes) =>
        megabytes < 0 ? megabytes : megabytes * 1024L * 1024L;

    private static async Task<QuotaHold> LockAndAssertAsync(
        BusinessOSDbContext db,
        IReadOnlyDictionary<Guid, QuotaDelta> demand,
        CancellationToken cancellationToken)
    {
        var hold = new QuotaHold { OwnsDepth = true };
        Depth.Value++;
        try
        {
            var tenants = demand.Keys.Where(x => x != Guid.Empty).OrderBy(x => x).ToArray();
            await AcquireLocksAsync(db, hold, tenants, cancellationToken);

            foreach (var tenantId in tenants)
            {
                var limits = await TryGetLimitsAsync(db, tenantId, cancellationToken);
                if (limits is null)
                    continue;

                var usage = await MeasureAsync(db, tenantId, cancellationToken);
                var delta = demand[tenantId];
                AssertTenant(limits, usage, delta);
            }

            return hold;
        }
        catch
        {
            hold.Success = false;
            await hold.DisposeAsync();
            throw;
        }
    }

    private static void AssertTenant(TenantLimits limits, ResourceUsage usage, QuotaDelta delta)
    {
        ThrowIfExceeded(QuotaResource.Users, usage.OccupiedSeats, delta.Users, limits.MaxUsers);
        ThrowIfExceeded(QuotaResource.Customers, usage.Customers, delta.Customers, limits.MaxCustomers);
        ThrowIfExceeded(QuotaResource.Products, usage.Products, delta.Products, limits.MaxProducts);
        ThrowIfExceeded(QuotaResource.Projects, usage.Projects, delta.Projects, limits.MaxProjects);
        ThrowIfExceeded(QuotaResource.Tasks, usage.Tasks, delta.Tasks, limits.MaxTasks);

        var maxBytes = MegabytesToBytes(limits.MaxStorageMb);
        if (Exceeds(usage.StorageBytes, delta.StorageBytes, maxBytes))
        {
            throw new PlanLimitExceededException(
                "Storage",
                ToDisplayMb(usage.StorageBytes),
                limits.MaxStorageMb);
        }
    }

    private static async Task AcquireLocksAsync(
        BusinessOSDbContext db,
        QuotaHold hold,
        IReadOnlyList<Guid> tenants,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            foreach (var tenantId in tenants)
            {
                var gate = ProcessLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync(cancellationToken);
                hold.Gates.Add(gate);
            }

            return;
        }

        if (db.Database.CurrentTransaction is null)
        {
            hold.Transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            hold.OwnsTransaction = true;
        }

        if (db.Database.IsSqlServer())
        {
            foreach (var tenantId in tenants)
                await AcquireSqlAppLockAsync(db, tenantId, cancellationToken);
        }
    }

    private static async Task AcquireSqlAppLockAsync(
        BusinessOSDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 15000;
            SELECT @result;
            """;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = "bos-quota-" + tenantId.ToString("N");
        command.Parameters.Add(parameter);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var code = Convert.ToInt32(scalar, System.Globalization.CultureInfo.InvariantCulture);
        if (code < 0)
        {
            throw new ConflictException(
                "Another change for this workspace is still being saved. Wait a moment and try again.");
        }
    }

    private static string ResourceName(QuotaResource resource) => resource switch
    {
        QuotaResource.Users => "Users",
        QuotaResource.Customers => "Customers",
        QuotaResource.Products => "Products",
        QuotaResource.Projects => "Projects",
        QuotaResource.Tasks => "Tasks",
        QuotaResource.AiRequests => "AI requests",
        QuotaResource.Storage => "Storage",
        _ => resource.ToString()
    };

    internal static void ExitDepth()
    {
        if (Depth.Value > 0)
            Depth.Value--;
    }
}

internal sealed class QuotaHold : IAsyncDisposable
{
    public static QuotaHold Noop { get; } = new() { IsNoop = true };

    public bool IsNoop { get; private init; }
    public bool OwnsDepth { get; init; }
    public bool OwnsTransaction { get; set; }
    public IDbContextTransaction? Transaction { get; set; }
    public List<SemaphoreSlim> Gates { get; } = [];
    public bool Success { get; set; }
    private bool _disposed;

        public void MarkSuccess()
        {
            if (!IsNoop)
                Success = true;
        }

    public async ValueTask DisposeAsync()
    {
        if (_disposed || IsNoop)
            return;

        _disposed = true;
        try
        {
            if (OwnsTransaction && Transaction is not null)
            {
                if (Success)
                    await Transaction.CommitAsync();
                else
                    await Transaction.RollbackAsync();
            }
        }
        finally
        {
            if (OwnsTransaction && Transaction is not null)
                await Transaction.DisposeAsync();

            foreach (var gate in Gates)
                gate.Release();

            if (OwnsDepth)
                QuotaEnforcer.ExitDepth();
        }
    }
}

internal sealed class QuotaDelta
{
    public int Users { get; set; }
    public int Customers { get; set; }
    public int Products { get; set; }
    public int Projects { get; set; }
    public int Tasks { get; set; }
    public long StorageBytes { get; set; }

    public bool HasPositive =>
        Users > 0 || Customers > 0 || Products > 0 || Projects > 0 || Tasks > 0 || StorageBytes > 0;
}

internal static class QuotaDemand
{
    public static IReadOnlyDictionary<Guid, QuotaDelta> Collect(BusinessOSDbContext db)
    {
        var map = new Dictionary<Guid, QuotaDelta>();

        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            switch (entry.Entity)
            {
                case ApplicationUser user:
                    Add(map, user.TenantId).Users += BoolDelta(entry, nameof(ApplicationUser.IsActive), user.IsActive);
                    break;
                case TeamInvitation invitation:
                    Add(map, invitation.TenantId).Users += InvitationDelta(entry, invitation);
                    break;
                case Customer customer:
                    Add(map, customer.TenantId).Customers += ExistenceDelta(entry, !customer.IsDeleted);
                    break;
                case Product product:
                    Add(map, product.TenantId).Products += ExistenceDelta(entry, !product.IsDeleted);
                    break;
                case Project project:
                    Add(map, project.TenantId).Projects += ExistenceDelta(entry, !project.IsDeleted);
                    break;
                case WorkTask task:
                    Add(map, task.TenantId).Tasks += ExistenceDelta(entry, !task.IsDeleted);
                    break;
                case GeneratedReport report:
                    Add(map, report.TenantId).StorageBytes += ByteDelta(
                        entry,
                        nameof(GeneratedReport.FileContent),
                        nameof(GeneratedReport.IsDeleted),
                        report.IsDeleted ? 0 : report.FileContent?.LongLength ?? 0);
                    break;
                case AiDocument document:
                    Add(map, document.TenantId).StorageBytes += ByteDelta(
                        entry,
                        nameof(AiDocument.Content),
                        nameof(AiDocument.IsDeleted),
                        document.IsDeleted ? 0 : Encoding.UTF8.GetByteCount(document.Content ?? string.Empty));
                    break;
            }
        }

        return map;
    }

    private static QuotaDelta Add(Dictionary<Guid, QuotaDelta> map, Guid tenantId)
    {
        if (!map.TryGetValue(tenantId, out var delta))
        {
            delta = new QuotaDelta();
            map[tenantId] = delta;
        }

        return delta;
    }

    private static int BoolDelta(EntityEntry entry, string propertyName, bool current)
    {
        if (entry.State is EntityState.Detached or EntityState.Unchanged)
            return 0;

        var before = entry.State == EntityState.Added
            ? false
            : entry.Property(propertyName).OriginalValue is true;
        if (before == current)
            return 0;

        return current ? 1 : -1;
    }

    private static int ExistenceDelta(EntityEntry entry, bool countsNow)
    {
        if (entry.State is EntityState.Detached or EntityState.Unchanged)
            return 0;

        var countedBefore = entry.State != EntityState.Added && OriginalCounts(entry);
        if (countedBefore == countsNow)
            return 0;

        return countsNow ? 1 : -1;
    }

    private static bool OriginalCounts(EntityEntry entry)
    {
        var deleted = entry.Properties.FirstOrDefault(x => x.Metadata.Name == "IsDeleted");
        return deleted?.OriginalValue is not true;
    }

    private static int InvitationDelta(EntityEntry entry, TeamInvitation invitation)
    {
        if (entry.State is EntityState.Detached or EntityState.Unchanged)
            return 0;

        var pendingNow = IsPending(invitation.Status, invitation.ExpiresAt, invitation.IsDeleted);
        var pendingBefore = entry.State != EntityState.Added && OriginalPending(entry);
        if (pendingBefore == pendingNow)
            return 0;

        return pendingNow ? 1 : -1;
    }

    private static bool OriginalPending(EntityEntry entry)
    {
        var status = entry.Property(nameof(TeamInvitation.Status)).OriginalValue;
        var expires = entry.Property(nameof(TeamInvitation.ExpiresAt)).OriginalValue;
        var deleted = entry.Property(nameof(TeamInvitation.IsDeleted)).OriginalValue is true;
        return status is InvitationStatus pending &&
            pending == InvitationStatus.Pending &&
            expires is DateTime expiry &&
            IsPending(InvitationStatus.Pending, expiry, deleted);
    }

    private static bool IsPending(InvitationStatus status, DateTime expiresAt, bool isDeleted) =>
        !isDeleted && status == InvitationStatus.Pending && expiresAt > DateTime.UtcNow;

    private static long ByteDelta(EntityEntry entry, string contentProperty, string deletedProperty, long currentBytes)
    {
        if (entry.State is EntityState.Detached or EntityState.Unchanged)
            return 0;

        if (entry.State == EntityState.Added)
            return Math.Max(0, currentBytes);

        var wasDeleted = entry.Property(deletedProperty).OriginalValue is true;
        var originalBytes = wasDeleted ? 0 : OriginalContentLength(entry.Property(contentProperty).OriginalValue);
        return currentBytes - originalBytes;
    }

    private static long OriginalContentLength(object? value) => value switch
    {
        byte[] bytes => bytes.LongLength,
        string text => Encoding.UTF8.GetByteCount(text),
        _ => 0
    };
}

internal static class QuotaDemandExtensions
{
    public static bool HasPositiveDemand(this IReadOnlyDictionary<Guid, QuotaDelta> demand) =>
        demand.Values.Any(x => x.HasPositive);
}
