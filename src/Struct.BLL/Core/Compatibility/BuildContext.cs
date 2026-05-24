using Struct.DAL.Models;

namespace Struct.BLL.Core.Compatibility;

/* build-in memory model */
public class BuildContext
{
    public Component? Cpu { get; set; }
    public Component? Gpu { get; set; }
    public Component? Motherboard { get; set; }
    public Component? Ram { get; set; }
    public Component? Psu { get; set; }
    public Component? Case { get; set; }
    public Component? Cooler { get; set; }
    public Component? Storage { get; set; }
}