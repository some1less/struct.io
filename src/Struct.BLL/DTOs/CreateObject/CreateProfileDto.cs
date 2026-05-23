namespace Struct.BLL.DTOs;

public class CreateProfileDto
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Description { get; set; }
}
