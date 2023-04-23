using Grace.DependencyInjection;

namespace Roadnik.MAUI.Toolkit;

public abstract class CContentPage : ContentPage
{
  protected CContentPage()
  {
    Container = MauiProgram.Container;
    Container.Inject(this);
  }

  public IInjectionScope Container { get; }

}