namespace Struct.DAL.Models;

public class Profile
{
    public int Id { get; set; }

    public required string FirstName { get; set; }
    public required string LastName { get; set; }

    public string? Description { get; set; }

    /* reversed connection */
    public Account? Account { get; set; } // 1:1 
    public ICollection<SavedBuild> SavedBuilds { get; set; } = new List<SavedBuild>(); // 1:N saved build by User
}