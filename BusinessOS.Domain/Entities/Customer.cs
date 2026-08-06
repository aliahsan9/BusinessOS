using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Customer or client record managed by a tenant for sales, orders, and invoicing.
/// </summary>
/// <remarks>
/// Tenant-scoped. Owns a collection of <see cref="Order"/> records placed by this customer.
/// Contact details are required; optional fields support CRM notes and user assignment.
/// </remarks>
public class Customer : AuditableEntity
{
    /// <summary>
    /// Identifier of the tenant that owns this customer. Required for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Customer's first name. Required.
    /// </summary>
    public string FirstName { get; set; } = default!;

    /// <summary>
    /// Customer's last name. Required.
    /// </summary>
    public string LastName { get; set; } = default!;

    /// <summary>
    /// Primary email address for the customer. Required and expected to be valid for communication.
    /// </summary>
    public string Email { get; set; } = default!;

    /// <summary>
    /// Primary phone number for the customer. Required.
    /// </summary>
    public string PhoneNumber { get; set; } = default!;

    /// <summary>
    /// Street address line for the customer. Required.
    /// </summary>
    public string Address { get; set; } = default!;

    /// <summary>
    /// City component of the customer's address. Required.
    /// </summary>
    public string City { get; set; } = default!;

    /// <summary>
    /// Country component of the customer's address. Required.
    /// </summary>
    public string Country { get; set; } = default!;

    /// <summary>
    /// Postal or ZIP code for the customer's address. Required.
    /// </summary>
    public string PostalCode { get; set; } = default!;

    /// <summary>
    /// Optional company or organization name when the customer represents a business account.
    /// </summary>
    public string? Company { get; set; }

    /// <summary>
    /// Optional free-text notes about the customer for internal CRM use.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Indicates whether the customer is active and available for new orders. Defaults to true.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional identity user identifier of the team member assigned to manage this customer.
    /// </summary>
    public string? AssignedUserId { get; set; }

    /// <summary>
    /// Collection of orders placed by this customer.
    /// </summary>
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
