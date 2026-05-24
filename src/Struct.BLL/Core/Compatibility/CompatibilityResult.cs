namespace Struct.BLL.Core.Compatibility;

public class CompatibilityResult
{
    public bool IsCompatible => !Violations.Any();
    public List<string> Violations { get; set; } = new();
}