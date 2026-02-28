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
    if (!statusBarBehaviorApplied && Application.Current != null)
    {
      var theme = Application.Current.RequestedTheme;
      if (theme == AppTheme.Unspecified)
        theme = AppTheme.Dark;

      Behaviors.Add(new StatusBarBehavior
      {
        StatusBarColor = theme == AppTheme.Dark ? Color.FromRgba(0f, 0f, 0f, 0f) : Color.FromRgba(255f, 255f, 255f, 0f),
        StatusBarStyle = theme == AppTheme.Dark ? StatusBarStyle.LightContent : StatusBarStyle.DarkContent,
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