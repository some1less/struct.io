namespace Struct.BLL.DTOs;

public class BuildComponentDto
{
    public int Id { get; set; }
    public int Quantity { get; set; }

    public int ComponentId { get; set; }
    public int BuildId { get; set; }
}
