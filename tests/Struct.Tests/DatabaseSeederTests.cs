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
        [new CpuParser(), new StorageParser()],
        new PriceBoundsSettings { Fallback = new PriceRange { Min = 1, Max = 100_000 } },
        NullLogger<DatabaseSeeder>.Instance);

    [Fact]
    public void ParseCatalog_KeepsRowsWithAParser_AndSkipsTheRest()
    {
        const string json = """
            [
              { "Category": "Cpu", "Name": "AMD Ryzen 5 7600", "Brand": "AMD", "PricePln": "899",
                "TechnicalSpecs": { "Socket": "AM5" } },
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
        Assert.Equal(Category.Hdd, components[1].Category);
    }

    [Fact]
    public void ParseCatalog_NotAnArray_Throws()
    {
        Assert.Throws<InvalidDataException>(() => Seeder.ParseCatalog("null"));
    }
}
