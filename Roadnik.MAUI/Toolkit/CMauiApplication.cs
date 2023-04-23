using Ax.Fw.SharedTypes.Interfaces;
using Grace.DependencyInjection;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Toolkit;

public abstract class CMauiApplication : Application, IMauiApp
{
  protected CMauiApplication() : base()
  {
    Container = MauiProgram.Container;
    var lifetime = Container.Locate<IReadOnlyLifetime>();
    lifetime.DoOnCompleted(Quit);
  }

  public IInjectionScope Container { get; }

}
