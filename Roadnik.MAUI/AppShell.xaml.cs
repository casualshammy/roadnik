using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui.Alerts;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI;

public partial class NavigationAppShell : Shell
{
  private readonly ILifetime p_lifetime;
  private readonly IPagesController p_pageController;
  private DateTimeOffset p_lastTimeBackClicked = DateTimeOffset.MinValue;

  public NavigationAppShell()
  {
    Console.WriteLine("NavigationAppShell is started");
    InitializeComponent();
    if (Application.Current is not IMauiApp app)
      throw new ApplicationException($"App is not '{nameof(IMauiApp)}'");

    p_lifetime = app.Container.Locate<ILifetime>();
    p_pageController = app.Container.Locate<IPagesController>();
  }

  protected override bool OnBackButtonPressed()
  {
    var mainPage = p_pageController.MainPage;
    var currentPage = p_pageController.CurrentPage;
    if (mainPage == currentPage && !Current.FlyoutIsPresented)
    {
      var now = DateTimeOffset.UtcNow;
      if (now - p_lastTimeBackClicked < TimeSpan.FromSeconds(3))
      {
        p_lifetime.Complete();
        return false;
      }

      p_lastTimeBackClicked = now;
      Toast
        .Make("Press back again to exit", CommunityToolkit.Maui.Core.ToastDuration.Short)
        .Show();
    }

    Current.CurrentItem = p_mainPageFlyoutItem;
    Current.FlyoutIsPresented = false;

    return true;
  }

}