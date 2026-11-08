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
        [new CpuParser()],
        new PriceBoundsSettings { Fallback = new PriceRange { Min = 1, Max = 100_000 } },
        NullLogger<DatabaseSeeder>.Instance);

    [Fact]
    public void ParseCatalog_KeepsRowsWithAParser_AndSkipsTheRest()
    {
        const string json = """
            [
              { "Category": "Cpu", "Name": "AMD Ryzen 5 7600", "Brand": "AMD", "PricePln": "899",
                "TechnicalSpecs": { "Socket": "AM5" } },
              { "Category": "Fan", "Name": "Some Fan" },
              { "Name": "No Category" }
            ]
            """;

        var component = Assert.Single(Seeder.ParseCatalog(json));

        Assert.Equal(Category.Cpu, component.Category);
        Assert.Equal(899m, component.Price);
        Assert.Equal("DDR5", component.TechnicalSpecs[Spec.MemoryType]);
    }

    [Fact]
    public void ParseCatalog_NotAnArray_Throws()
    {
        Assert.Throws<InvalidDataException>(() => Seeder.ParseCatalog("null"));
    }
}
