namespace Struct.BLL.DTOs;

public class CreateComponentDto
{
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string Brand { get; set; }
    public decimal Price { get; set; }
    public Dictionary<string, string> TechnicalSpecs { get; set; } = new();
}
