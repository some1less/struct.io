namespace Struct.DAL.Models;

public class BuildComponent
{
    public int Id { get; set; }

    public int Quantity { get; set; }

    /* FKs */
    public int ComponentId { get; set; }
    public Component Component { get; set; } = null!;

    public int BuildId { get; set; }
    public SavedBuild Build { get; set; } = null!;

}