using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;

namespace Roadnik.MAUI;

public partial class App : ContainerizedMauiApplication
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
      storage.SetValue(storage.SERVER_KEY, "example-key");
      storage.SetValue(storage.TIME_INTERVAL, 15);
      storage.SetValue(storage.DISTANCE_INTERVAL, 100);
    }
  }

}