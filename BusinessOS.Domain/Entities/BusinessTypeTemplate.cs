using BusinessOS.Domain.Common;

namespace BusinessOS.Domain.Entities;

/// <summary>
/// Platform catalog of company templates. Not tenant-owned.
/// </summary>
public class BusinessTypeTemplate : BaseEntity
{
    public string Key { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string ModulesJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
