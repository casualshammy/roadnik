using Grace.DependencyInjection;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Toolkit;

public abstract class ContainerizedContentPage : ContentPage
{
  protected ContainerizedContentPage()
  {
    if (Application.Current is not IMauiApp app)
      throw new ApplicationException($"Application is not {nameof(ContainerizedMauiApplication)}");

    Container = app.Container;

    Container.Inject(this);
  }

  public IInjectionScope Container { get; }

}