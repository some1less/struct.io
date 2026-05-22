namespace Struct.BLL.DTOs;

public class ComponentDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string Brand { get; set; }
    public Dictionary<string, string> TechnicalSpecs { get; set; } = new();
}