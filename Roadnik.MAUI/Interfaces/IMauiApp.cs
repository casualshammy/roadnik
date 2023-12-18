using Ax.Fw.SharedTypes.Interfaces;

namespace Roadnik.MAUI.Interfaces;

public interface IMauiApp
{
  IReadOnlyDependencyContainer Container { get; }
}
