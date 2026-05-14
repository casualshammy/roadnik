using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Toolkit;

public abstract class CMauiApplication : Application, IMauiApp
{
  protected CMauiApplication() : base()
  {
    Container = MauiProgram.Container;
  }

  public IReadOnlyDependencyContainer Container { get; }

}
