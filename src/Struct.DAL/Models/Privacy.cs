namespace Struct.DAL.Models;

public class Privacy
{
    public int Id { get; set; }
    public required string Name { get; set; }

    /* reversed connection */
    public ICollection<SavedBuild> SavedBuilds { get; set; } = new List<SavedBuild>();

}