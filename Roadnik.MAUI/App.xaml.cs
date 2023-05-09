using Ax.Fw;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using static Roadnik.MAUI.Data.Consts;

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
    if (storage.GetValueOrDefault<bool>(PREF_INITIALIZED) != true)
    {
      storage.SetValue(PREF_INITIALIZED, true);
      storage.SetValue(PREF_SERVER_ADDRESS, "https://roadnik.axio.name");
      storage.SetValue(PREF_SERVER_KEY, Utilities.GetRandomString(10, false));
      storage.SetValue(PREF_TIME_INTERVAL, 15);
      storage.SetValue(PREF_DISTANCE_INTERVAL, 100);
      storage.SetValue(PREF_TRACKPOINT_REPORTING_CONDITION, TrackpointReportingConditionType.TimeAndDistance);
      storage.SetValue(PREF_USER_MSG, "");
      storage.SetValue(PREF_MIN_ACCURACY, 30);
      storage.SetValue(PREF_NICKNAME, $"user-{Random.Shared.Next(100000, 999999)}");
      storage.SetValue(PREF_MAP_OPEN_BEHAVIOR, MapOpeningBehavior.AllTracks);
    }
  }

}