namespace BusinessOS.Domain.Entities;

public class UserRole
{
    public string UserId { get; set; } = default!;

    public Guid RoleId { get; set; }

    public Role Role { get; set; } = default!;
}
