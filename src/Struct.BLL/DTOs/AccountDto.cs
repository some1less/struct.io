namespace Struct.BLL.DTOs;

public class AccountDto
{
    public int Id { get; set; }
    public required string Nickname { get; set; }
    public required string Email { get; set; }

    public int ProfileId { get; set; }
    public int RoleId { get; set; }
}
