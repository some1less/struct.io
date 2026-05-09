namespace Struct.DAL.Models;

public class Component
{
    
    public int Id { get; set; }
    
    public required string Name { get; set; }
    public Category Category { get; set; }
    
    public required string Brand { get; set; }
    
    public required Dictionary<string, string> TechnicalSpecs { get; set; } = []; 
    
    
}