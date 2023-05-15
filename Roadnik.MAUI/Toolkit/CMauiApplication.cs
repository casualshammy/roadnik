using Grace.DependencyInjection;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Toolkit;

public abstract class CMauiApplication : Application, IMauiApp
{
  protected CMauiApplication() : base()
  {
    Container = MauiProgram.Container;
  }

  public IInjectionScope Container { get; }

}
