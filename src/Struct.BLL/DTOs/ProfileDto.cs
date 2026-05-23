namespace Struct.BLL.DTOs;

public class ProfileDto
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Description { get; set; }
}
