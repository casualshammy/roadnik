using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Toolkit;

public abstract class CContentPage : ContentPage
{
  private readonly IPagesController p_pageController;

  protected CContentPage()
  {
    Container = MauiProgram.Container;
    p_pageController = Container.Locate<IPagesController>();
  }

  public IReadOnlyDependencyContainer Container { get; }

  protected override void OnAppearing()
  {
    base.OnAppearing();
    p_pageController.OnPageActivated(this);
  }

}