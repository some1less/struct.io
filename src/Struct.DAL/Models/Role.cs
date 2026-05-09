namespace Struct.DAL.Models;

public class Role
{
    public int Id { get; set; }
    public required string Name { get; set; }

    /* reversed connection */
    public ICollection<Account> Accounts { get; set; } = new List<Account>(); // 1:N

}