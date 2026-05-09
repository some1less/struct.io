namespace Struct.DAL.Models;

public class SavedBuild
{
    public int Id { get; set; }

    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }

    /* FKs */
    public int ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;

    public int PrivacyId { get; set; }
    public Privacy Privacy { get; set; } = null!;

    /* reversed connection */
    public ICollection<BuildComponent> BuildComponents { get; set; } = new List<BuildComponent>();
}