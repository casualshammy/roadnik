using Roadnik.Common.Toolkit;
using Roadnik.MAUI.Data;
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
    p_notifyNewUser.SwitchIsToggled = p_bindingCtx.NotificationOnNewUser;
  }
    
}
