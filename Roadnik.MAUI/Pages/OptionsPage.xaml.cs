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

    p_mapCacheToggle.SwitchIsToggled = p_bindingCtx.MapCacheEnabled;
    p_notifyNewTrack.SwitchIsToggled = p_bindingCtx.NotificationOnNewTrack;
    p_notifyNewPoint.SwitchIsToggled = p_bindingCtx.NotificationOnNewPoint;
  }
    
}
