using Microsoft.EntityFrameworkCore;

namespace Struct.DAL.Models;

[Index(nameof(Name), IsUnique = true)]
public class Component
{

    public int Id { get; set; }

    public required string Name { get; set; }
    public required Category Category { get; set; }
    public required string Brand { get; set; }

    public required Dictionary<string, string> TechnicalSpecs { get; set; } = [];

    /* reversed connection */
    public ICollection<BuildComponent> BuildComponents { get; set; } = new List<BuildComponent>();
}