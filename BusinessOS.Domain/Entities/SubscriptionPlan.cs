using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

public class SubscriptionPlan : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public int MaxUsers { get; set; } = 5;
    public int MaxCustomers { get; set; } = 100;
    public int MaxProjects { get; set; } = 10;
    public long MaxStorageMb { get; set; } = 1024;
    public int MaxAiRequests { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
