using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Struct.API.Extensions.Seeding;
using Struct.API.Extensions.Seeding.Parsers;
using Struct.BLL.Core.Specs;
using Struct.DAL.Context;
using Struct.DAL.Models;

namespace Struct.Tests;

public class DatabaseSeederTests
{
    // ParseCatalog never touches the database, so the context needs no provider.
    private static readonly DatabaseSeeder Seeder = new(
        new AppDbContext(new DbContextOptions<AppDbContext>()),
        [new CpuParser(), new GpuParser(), new StorageParser()],
        NullLogger<DatabaseSeeder>.Instance);

    [Fact]
    public void ParseCatalog_KeepsRowsWithAParser_AndSkipsTheRest()
    {
        const string json = """
            [
              { "Category": "Cpu", "Name": "AMD Ryzen 5 7600", "Brand": "AMD", "PricePln": "899",
                "SourceUrl": "https://www.x-kom.pl/p/1.html", "TechnicalSpecs": { "Socket": "AM5" } },
              { "Category": "Hdd", "Name": "Seagate 2TB", "PricePln": "250",
                "TechnicalSpecs": { "Type": "HDD", "Capacity": "2000" } },
              { "Category": "Fan", "Name": "Some Fan" },
              { "Name": "No Category" },
              null
            ]
            """;

        var components = Seeder.ParseCatalog(json);

        Assert.Equal(2, components.Count);
        var cpu = components[0];
        Assert.Equal(Category.Cpu, cpu.Category);
        Assert.Equal(899m, cpu.Price);
        Assert.Equal("DDR5", cpu.TechnicalSpecs[Spec.MemoryType]);
        Assert.Equal("https://www.x-kom.pl/p/1.html", cpu.SourceUrl);
        Assert.Equal(Category.Hdd, components[1].Category);
        Assert.Null(components[1].SourceUrl);
    }

    [Fact]
    public void ParseCatalog_KeepsGpuChipset_AndLeavesMissingSpecsOut()
    {
        const string json = """
            [
              { "Category": "Gpu", "Name": "Zotac GeForce RTX 5060 AMP Core 8GB", "Brand": "Zotac", "PricePln": "2399",
                "TechnicalSpecs": { "Chipset": "GeForce RTX 5060", "VRAM": "8", "TDP": "145", "CoreClock": "" } }
            ]
            """;

        var specs = Seeder.ParseCatalog(json).Single().TechnicalSpecs;

        // BenchmarkScores matches PassMark by Chipset first; an absent Length must stay absent, not become "0".
        Assert.Equal("GeForce RTX 5060", specs[Spec.Chipset]);
        Assert.Equal([Spec.Chipset, Spec.Vram, Spec.Tdp], specs.Keys);
    }

    [Fact]
    public void ParseCatalog_SkipsRowsWithoutARealPrice_AndKeepsTheShopPriceAsIs()
    {
        const string json = """
            [
              { "Category": "Gpu", "Name": "MSI GeForce RTX 5090 Gaming Trio OC 32GB", "PricePln": "45999",
                "TechnicalSpecs": { "Chipset": "GeForce RTX 5090" } },
              { "Category": "Gpu", "Name": "Old USD-only row", "Price": "499", "TechnicalSpecs": {} },
              { "Category": "Gpu", "Name": "Zero price", "PricePln": "0", "TechnicalSpecs": {} }
            ]
            """;

        var component = Seeder.ParseCatalog(json).Single();

        Assert.Equal(45999m, component.Price);
    }

    [Fact]
    public void ParseCatalog_NotAnArray_Throws()
    {
        Assert.Throws<InvalidDataException>(() => Seeder.ParseCatalog("null"));
    }

    [Fact]
    public void PrepareCatalog_WithNoUsableRows_Throws()
    {
        Assert.Throws<InvalidDataException>(() =>
            Seeder.PrepareCatalog("""[{ "Category": "Gpu", "Name": "No price", "TechnicalSpecs": {} }]""", null));
    }
}
