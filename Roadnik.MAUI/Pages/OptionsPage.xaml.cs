using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.Toolkit;
using Roadnik.MAUI.ViewModels;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Pages;

public partial class OptionsPage : CContentPage
{
  private readonly OptionsPageViewModel p_bindingCtx;
  private readonly IPreferencesStorage p_storage;

  public OptionsPage()
  {
    InitializeComponent();
    p_bindingCtx = (OptionsPageViewModel)BindingContext;
    p_storage = Container.Locate<IPreferencesStorage>();

    p_deleteOldRouteOnNew.SwitchIsToggled = p_bindingCtx.WipeOldTrackOnNewEnabled;
    p_locationProvidersGps.SwitchIsToggled = p_bindingCtx.LocationProviderGpsEnabled;
    p_locationProvidersNetwork.SwitchIsToggled = p_bindingCtx.LocationProviderNetworkEnabled;
    p_locationProvidersPassive.SwitchIsToggled = p_bindingCtx.LocationProviderPassiveEnabled;
    p_notifyNewTrack.SwitchIsToggled = p_bindingCtx.NotificationOnNewTrack;
    p_notifyNewPoint.SwitchIsToggled = p_bindingCtx.NotificationOnNewPoint;
    p_hrReporting.SwitchIsToggled = p_bindingCtx.BleHrmEnabled;
    p_displayOnLockScreen.SwitchIsToggled = p_bindingCtx.DisplayOnLockScreenEnabled;

    p_bleHrmDevice.TapCommand = new Command(OnBleHrmDeviceChanged);
  }

  private async void OnBleHrmDeviceChanged(object? _arg)
  {
    var bleWindowModel = new BleDevicesPageViewModel(
      _device =>
      {
        if (_device == null)
          p_storage.SetValue(PREF_BLE_HRM_DEVICE_INFO, (HrmDeviceInfo?)null);
        else
          p_storage.SetValue(PREF_BLE_HRM_DEVICE_INFO, new HrmDeviceInfo(_device.Id, _device.Name ?? string.Empty));
      },
      () => Navigation.PopModalAsync());

    await Navigation.PushModalAsync(new BleDevicesPage(bleWindowModel));
  }

}
