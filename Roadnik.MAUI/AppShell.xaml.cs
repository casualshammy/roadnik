using Ax.Fw.SharedTypes.Interfaces;
using CommunityToolkit.Maui.Alerts;
using Roadnik.MAUI.Interfaces;

namespace Roadnik.MAUI;

public partial class NavigationAppShell : Shell
{
  private readonly ILifetime p_lifetime;
  private DateTimeOffset p_lastTimeBackClicked = DateTimeOffset.MinValue;

  public NavigationAppShell()
  {
    InitializeComponent();
    if (Application.Current is not IMauiApp app)
      throw new ApplicationException($"App is not '{nameof(IMauiApp)}'");

    p_lifetime = app.Container.Locate<ILifetime>();
  }

  protected override bool OnBackButtonPressed()
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

    return true;
  }

}