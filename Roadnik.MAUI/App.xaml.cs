﻿using Ax.Fw;
using JustLogger.Interfaces;
using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI;

public partial class App : CMauiApplication
{
  public App()
  {
    var log = Container.Locate<ILogger>();

    log.Info($"App is starting...");
    InitializeComponent();
    SetupDefaultPreferences();
    log.Info($"App is started");

    MainPage = new NavigationAppShell();
  }

  private void SetupDefaultPreferences()
  {
    var storage = Container.Locate<IPreferencesStorage>();
    if (storage.GetValueOrDefault<bool>(PREF_INITIALIZED) != true)
    {
      storage.SetValue(PREF_INITIALIZED, true);
      storage.SetValue(PREF_SERVER_ADDRESS, "https://roadnik.app");
      storage.SetValue(PREF_ROOM, Utilities.GetRandomString(10, false));
      storage.SetValue(PREF_TIME_INTERVAL, 10);
      storage.SetValue(PREF_DISTANCE_INTERVAL, 100);
      storage.SetValue(PREF_TRACKPOINT_REPORTING_CONDITION, TrackpointReportingConditionType.TimeAndDistance);
      storage.SetValue(PREF_USER_MSG, "Hi!");
      storage.SetValue(PREF_MIN_ACCURACY, 30);
      storage.SetValue(PREF_USERNAME, $"user-{Random.Shared.Next(100000, 999999)}");
      storage.SetValue(PREF_MAP_OPEN_BEHAVIOR, MapOpeningBehavior.AllTracks);
      storage.SetValue(PREF_NOTIFY_NEW_POINT, true);
      storage.SetValue(PREF_NOTIFY_NEW_TRACK, true);
      storage.SetValue(PREF_WIPE_OLD_TRACK_ON_NEW_ENABLED, true);
    }
  }

}