namespace BusinessOS.Domain.Billing;

/// <summary>
/// A resource whose creation is limited by the tenant's subscription.
/// </summary>
public enum QuotaResource
{
    Users = 0,
    Customers = 1,
    Products = 2,
    Projects = 3,
    Tasks = 4,
    AiRequests = 5,
    Storage = 6
}
