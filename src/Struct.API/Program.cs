using Microsoft.EntityFrameworkCore;
using Npgsql;
using Struct.API.Extensions.Seeding;
using Struct.API.Extensions.Seeding.Parsers;
using Struct.BLL.Services;
using Struct.DAL.Context;
using Struct.DAL.Repositories;
using Struct.DAL.Repositories.Implementations;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("DefaultConnection"));
dataSourceBuilder.EnableDynamicJson(); // fixing error for casting Dictionary within the Postgres
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource));

/* CRUD */
// DAL Registration
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IPrivacyRepository, PrivacyRepository>();
builder.Services.AddScoped<IComponentRepository, ComponentRepository>();
builder.Services.AddScoped<ISavedBuildRepository, SavedBuildRepository>();
builder.Services.AddScoped<IBuildComponentRepository, BuildComponentRepository>();

// BLL Registration
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IPrivacyService, PrivacyService>();
builder.Services.AddScoped<IComponentService, ComponentService>();
builder.Services.AddScoped<ISavedBuildService, SavedBuildService>();
builder.Services.AddScoped<IBuildComponentService, BuildComponentService>();

/* DATA SEEDING - ONES AND FORALL*/
builder.Services.AddTransient<IComponentParser, CpuParser>();
builder.Services.AddTransient<IComponentParser, GpuParser>();
builder.Services.AddTransient<IComponentParser, MotherboardParser>();
builder.Services.AddTransient<IComponentParser, RamParser>();
builder.Services.AddTransient<IComponentParser, PsuParser>();
builder.Services.AddTransient<IComponentParser, CaseParser>();
builder.Services.AddTransient<IComponentParser, StorageParser>();
builder.Services.AddTransient<IComponentParser, CoolerParser>();

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
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();