namespace BusinessOS.Domain.Enums;

public static class ProductKinds
{
    public const string Physical = "Physical";
    public const string Service = "Service";
}

public static class EmploymentStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string OnLeave = "OnLeave";
}

public static class EmploymentTypes
{
    public const string FullTime = "FullTime";
    public const string PartTime = "PartTime";
    public const string Contract = "Contract";
    public const string Intern = "Intern";
}

public static class LeaveStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

public static class LeadStatuses
{
    public const string New = "New";
    public const string Qualified = "Qualified";
    public const string Converted = "Converted";
    public const string Lost = "Lost";
}

public static class DealStatuses
{
    public const string Open = "Open";
    public const string Won = "Won";
    public const string Lost = "Lost";
}

public static class FollowUpStatuses
{
    public const string Open = "Open";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class TransferStatuses
{
    public const string Draft = "Draft";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class FulfillmentStatuses
{
    public const string Pending = "Pending";
    public const string Picking = "Picking";
    public const string Packed = "Packed";
    public const string Shipped = "Shipped";
    public const string Delivered = "Delivered";
    public const string Partial = "Partial";
}

public static class ReturnStatuses
{
    public const string Requested = "Requested";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Refunded = "Refunded";
}

public static class TimeEntryStatuses
{
    public const string Running = "Running";
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
}

public static class SoftwareStages
{
    public static readonly string[] Default =
    [
        "Backlog",
        "To Do",
        "Development",
        "Code Review",
        "QA Testing",
        "Ready for Deployment",
        "Deployed"
    ];
}
