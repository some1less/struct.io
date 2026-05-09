using Microsoft.EntityFrameworkCore;

namespace Struct.DAL.Models;

[Index(nameof(Nickname), IsUnique = true)]
[Index(nameof(Email), IsUnique = true)]
public class Account
{
    public int Id { get; set; }

    public required string Nickname { get; set; }
    public required string PasswordHash { get; set; }
    public required string Email { get; set; }

    /* FKs */
    public int ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}