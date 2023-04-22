using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui.Alerts;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI;

//public partial class AppShell : Shell
//{
//  public AppShell()
//  {
//    InitializeComponent();
//  }
//}

public partial class NavigationAppShell : Shell
{
  //private readonly Stack<ShellNavigationState> p_urls = new();
  //private ShellNavigationState? p_currentState;
  //private ShellNavigationState? p_previousPage;
  private readonly ILifetime p_lifetime;
  private DateTimeOffset p_lastTimeBackClicked = DateTimeOffset.MinValue;

  public NavigationAppShell()
  {
    InitializeComponent();
    if (Application.Current is not IMauiApp app)
      throw new ApplicationException($"App is not '{nameof(IMauiApp)}'");

    p_lifetime = app.Container.Locate<ILifetime>();
  }

  protected override void OnNavigated(ShellNavigatedEventArgs _args)
  {
    base.OnNavigated(_args);
    //if (_args.Previous == null)
    //  return;

    //if (_args.Previous == p_previousPage)
    //  return;

    //if (_args.Previous == p_currentState)
    //  return;

    //p_urls.Push(_args.Previous);
    //p_previousPage = _args.Previous;
    //p_currentState = _args.Current;
  }

  protected override bool OnBackButtonPressed()
  {
    //if (!p_urls.TryPop(out var page))
    //  return false;

    //Current.GoToAsync(page);

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

    return true;
  }

}