using Struct.DAL.Models;

namespace Struct.Tests.TestSupport;

/// <summary>
/// Fluent builder for synthetic <see cref="Component"/> instances used across the
/// engine tests. Keeps every test readable by only stating the specs it actually cares about.
/// </summary>
public sealed class ComponentBuilder
{
    private string _name = "Generic Component";
    private Category _category = Category.Cpu;
    private string _brand = "Generic";
    private decimal _price;
    private readonly Dictionary<string, string> _specs = new();

    public static ComponentBuilder New(Category category, string name) =>
        new() { _category = category, _name = name };

    public ComponentBuilder Brand(string brand) { _brand = brand; return this; }
    public ComponentBuilder Price(decimal price) { _price = price; return this; }
    public ComponentBuilder Spec(string key, string value) { _specs[key] = value; return this; }

    public Component Build() => new()
    {
        Name = _name,
        Category = _category,
        Brand = _brand,
        Price = _price,
        TechnicalSpecs = new Dictionary<string, string>(_specs)
    };

    public static implicit operator Component(ComponentBuilder b) => b.Build();
}
