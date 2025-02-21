using Ax.Fw.SharedTypes.Interfaces;
using Roadnik.MAUI.Toolkit;
using System.Reactive.Subjects;

namespace Roadnik.MAUI;

public partial class App : CMauiApplication
{
  private static readonly ReplaySubject<bool> p_windowActivated = new(1);
  private static readonly Subject<IButton> p_buttonLongPressed = new();

  public App()
  {
    var log = Container.Locate<ILog>();

    log.Info($"App is starting...");
    InitializeComponent();
    log.Info($"App is started");

    MainPage = new NavigationAppShell();

    Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("LongPressButton", (_handler, _view) =>
    {
      _handler.PlatformView.LongClick += (_o, _e) => p_buttonLongPressed.OnNext(_view);
    });
  }

  public static IObservable<bool> WindowActivated { get; } = p_windowActivated;
  public static IObservable<IButton> ButtonLongPressed { get; } = p_buttonLongPressed;

  protected override Window CreateWindow(IActivationState? _activationState)
  {
    Window window = base.CreateWindow(_activationState);

    window.Activated += (_s, _e) => p_windowActivated.OnNext(true);
    window.Deactivated += (_s, _e) => p_windowActivated.OnNext(false);

    return window;
  }

}