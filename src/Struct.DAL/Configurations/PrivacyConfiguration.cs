using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Struct.DAL.Models;

namespace Struct.DAL.Configurations;

public class PrivacyConfiguration : IEntityTypeConfiguration<Privacy>
{
    public void Configure(EntityTypeBuilder<Privacy> builder)
    {
        builder.HasData(
            new Privacy { Id = 1, Name = "Private" },
            new Privacy { Id = 2, Name = "Public" }
        );
    }
}