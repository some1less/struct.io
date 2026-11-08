using Struct.DAL.Models;

namespace Struct.BLL.Core.Compatibility;

public class BuildContext
{
    private readonly Dictionary<Category, Component> _slots;

    public BuildContext() => _slots = new();

    private BuildContext(Dictionary<Category, Component> slots) => _slots = new(slots);

    public Component? this[Category category] => _slots.GetValueOrDefault(SlotOf(category));

    public IEnumerable<Component> Components => _slots.Values;

    public void Assign(Component component) => _slots[SlotOf(component.Category)] = component;

    public BuildContext Clone() => new(_slots);

    public Component? Cpu { get => this[Category.Cpu]; init => Put(Category.Cpu, value); }
    public Component? Gpu { get => this[Category.Gpu]; init => Put(Category.Gpu, value); }
    public Component? Motherboard { get => this[Category.Motherboard]; init => Put(Category.Motherboard, value); }
    public Component? Ram { get => this[Category.Ram]; init => Put(Category.Ram, value); }
    public Component? Psu { get => this[Category.Psu]; init => Put(Category.Psu, value); }
    public Component? Case { get => this[Category.Case]; init => Put(Category.Case, value); }
    public Component? Cooler { get => this[Category.Cooler]; init => Put(Category.Cooler, value); }

    // A build has one storage drive: SSD and HDD compete for the same slot.
    private static Category SlotOf(Category category) => category == Category.Hdd ? Category.Ssd : category;

    private void Put(Category category, Component? component)
    {
        if (component != null) _slots[SlotOf(category)] = component;
    }
}
