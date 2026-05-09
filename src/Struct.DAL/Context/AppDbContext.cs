using Microsoft.EntityFrameworkCore;
using Struct.DAL.Models;

namespace Struct.DAL.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Account> Accounts { get; set; }
    public DbSet<Profile> Profiles { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Privacy> Privacies { get; set; }
    public DbSet<SavedBuild> SavedBuilds { get; set; }
    public DbSet<Component> Components { get; set; }
    public DbSet<BuildComponent> BuildComponents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        modelBuilder.Entity<Component>()
            .Property(c => c.TechnicalSpecs)
            .HasColumnType("jsonb");

        /* auto-assign current time when created */
        modelBuilder.Entity<SavedBuild>()
            .Property(b => b.CreatedAt)
            .HasDefaultValueSql("NOW()");

        /* 1:1 connection */
        modelBuilder.Entity<Account>()
            .HasOne(a => a.Profile)
            .WithOne(p => p.Account)
            .HasForeignKey<Account>(a => a.ProfileId);

        /* save Enums as a string */
        modelBuilder.Entity<Component>()
            .Property(c => c.Category)
            .HasConversion<string>();
    }
}