using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Struct.DAL.Models;

namespace Struct.DAL.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasData(
            new Role { Id = 1, Name = "User" },
            new Role { Id = 2, Name = "Moderator" },
            new Role { Id = 3, Name = "Admin" }
        );
    }
}