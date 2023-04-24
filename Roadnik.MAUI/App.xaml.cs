using Ax.Fw;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;

namespace Roadnik.MAUI;

public partial class App : CMauiApplication
{
  public App()
  {
    InitializeComponent();
    SetupDefaultPreferences();

    MainPage = new NavigationAppShell();
  }

  private void SetupDefaultPreferences()
  {
    var storage = Container.Locate<IPreferencesStorage>();
    if (storage.GetValueOrDefault<bool>(storage.INITIALIZED) != true)
    {
      storage.SetValue(storage.INITIALIZED, true);
      storage.SetValue(storage.SERVER_ADDRESS, "https://example.com:5544");
      storage.SetValue(storage.SERVER_KEY, Utilities.GetRandomString(8, false));
      storage.SetValue(storage.TIME_INTERVAL, 15);
      storage.SetValue(storage.DISTANCE_INTERVAL, 100);
      storage.SetValue(storage.TRACKPOINT_REPORTING_CONDITION, TrackpointReportingConditionType.TimeAndDistance);
    }
  }

}