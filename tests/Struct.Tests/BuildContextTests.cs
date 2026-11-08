using Struct.BLL.Core.Compatibility;
using Struct.DAL.Models;
using Struct.Tests.TestSupport;

namespace Struct.Tests;

public class BuildContextTests
{
    [Fact]
    public void Hdd_And_Ssd_Share_OneStorageSlot()
    {
        var ctx = new BuildContext();
        ctx.Assign(ComponentBuilder.New(Category.Ssd, "SSD"));
        ctx.Assign(ComponentBuilder.New(Category.Hdd, "HDD"));

        Assert.Equal("HDD", ctx[Category.Ssd]?.Name);
        Assert.Equal("HDD", ctx[Category.Hdd]?.Name);
        Assert.Single(ctx.Components);
    }

    [Fact]
    public void Clone_DoesNotShareSlots_WithOriginal()
    {
        var original = new BuildContext { Cpu = ComponentBuilder.New(Category.Cpu, "Old") };

        var clone = original.Clone();
        clone.Assign(ComponentBuilder.New(Category.Cpu, "New"));
        clone.Assign(ComponentBuilder.New(Category.Gpu, "GPU"));

        Assert.Equal("Old", original.Cpu?.Name);
        Assert.Null(original.Gpu);
        Assert.Equal("New", clone.Cpu?.Name);
    }

    [Fact]
    public void EmptySlot_ReadsAsNull_AndIsNotListed()
    {
        var ctx = new BuildContext { Gpu = null };

        Assert.Null(ctx[Category.Gpu]);
        Assert.Empty(ctx.Components);
    }
}
