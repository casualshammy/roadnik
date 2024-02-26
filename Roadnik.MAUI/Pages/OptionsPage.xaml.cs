using Roadnik.MAUI.Toolkit;
using Roadnik.MAUI.ViewModels;

namespace Roadnik.MAUI.Pages;

public partial class OptionsPage : CContentPage
{
  private readonly OptionsPageViewModel p_bindingCtx;

  public OptionsPage()
  {
    InitializeComponent();
    p_bindingCtx = (OptionsPageViewModel)BindingContext;

    p_lowPowerMode.SwitchIsToggled = p_bindingCtx.LowPowerModeEnabled;
    p_deleteOldRouteOnNew.SwitchIsToggled = p_bindingCtx.WipeOldTrackOnNewEnabled;
    p_notifyNewTrack.SwitchIsToggled = p_bindingCtx.NotificationOnNewTrack;
    p_notifyNewPoint.SwitchIsToggled = p_bindingCtx.NotificationOnNewPoint;
  }
    
}
