using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI.Toolkit;

public abstract class CContentPage : ContentPage
{
  private readonly IPagesController p_pageController;

  protected CContentPage()
  {
    Container = MauiProgram.Container;
    p_pageController = Container.Locate<IPagesController>();

    var statusBarBehaviorApplied = Behaviors.OfType<StatusBarBehavior>().Any();
    if (!statusBarBehaviorApplied && App.Current != null && App.Current.Resources.TryGetValue("Primary", out var primaryColor))
    {
      Behaviors.Add(new StatusBarBehavior
      {
        StatusBarColor = (Color)primaryColor,
        StatusBarStyle = StatusBarStyle.Default
      });
    }
  }

  public IReadOnlyDependencyContainer Container { get; }

  protected override void OnAppearing()
  {
    base.OnAppearing();
    p_pageController.OnPageActivated(this);
  }

}