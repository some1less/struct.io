namespace Struct.BLL.DTOs;

public class CreateSavedBuildDto
{
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ProfileId { get; set; }
    public int PrivacyId { get; set; }
}
