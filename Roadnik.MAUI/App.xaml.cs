using JustLogger.Interfaces;
using Roadnik.MAUI.Toolkit;

namespace Roadnik.MAUI;

public partial class App : CMauiApplication
{
  public App()
  {
    var log = Container.Locate<ILogger>();

    log.Info($"App is starting...");
    InitializeComponent();
    log.Info($"App is started");

    MainPage = new NavigationAppShell();
  }

}