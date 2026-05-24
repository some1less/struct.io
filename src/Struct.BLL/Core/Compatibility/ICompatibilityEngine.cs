using Struct.DAL.Models;

namespace Struct.BLL.Core.Compatibility;

public interface ICompatibilityEngine
{
    CompatibilityResult CheckCompatibility(BuildContext currentBuild, Component candidate);
}