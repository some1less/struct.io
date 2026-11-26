namespace Struct.BLL.DTOs;

public class ComponentDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public required string Brand { get; set; }
    public decimal Price { get; set; }
    public string? SourceUrl { get; set; }
    public Dictionary<string, string> TechnicalSpecs { get; set; } = new();

}