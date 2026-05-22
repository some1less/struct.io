using Microsoft.EntityFrameworkCore;
using Npgsql;
using Struct.API.Extensions.Seeding;
using Struct.API.Extensions.Seeding.Parsers;
using Struct.DAL.Context;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddOpenApi();

var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
dataSourceBuilder.EnableDynamicJson(); // fixing error for casting Dictionary within the Postgres
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource));

/* DATA SEEDING - ONES AND FORALL*/
builder.Services.AddTransient<IComponentParser, CpuParser>();
builder.Services.AddTransient<IComponentParser, GpuParser>();
builder.Services.AddTransient<IComponentParser, MotherboardParser>();
builder.Services.AddTransient<IComponentParser, RamParser>();
builder.Services.AddTransient<IComponentParser, PsuParser>();
builder.Services.AddTransient<IComponentParser, CaseParser>();
builder.Services.AddTransient<IComponentParser, StorageParser>();

builder.Services.AddTransient<BuildCoresSeeder>();

var app = builder.Build();

/* ACTUAL DATABASE SEEDING */
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
        
        var buildCoresSeeder = services.GetRequiredService<BuildCoresSeeder>();
        
        string pathToOpenDb = Path.Combine(app.Environment.ContentRootPath, "Extensions", "Seeding", "open-db");
        await buildCoresSeeder.SeedFromDirectoryAsync(pathToOpenDb);
        
        logger.LogInformation("Database seeded successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during seeding.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();