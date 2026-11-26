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

    public decimal Price { get; set; }

    /// <summary>Shop product page the price was taken from; null for parts added by hand.</summary>
    public string? SourceUrl { get; set; }

    /* reversed connection */
    public ICollection<BuildComponent> BuildComponents { get; set; } = new List<BuildComponent>();
}