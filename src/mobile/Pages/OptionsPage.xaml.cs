using Roadnik.MAUI.Data;
using Roadnik.MAUI.Interfaces;
using Roadnik.MAUI.JsonCtx;
using Roadnik.MAUI.Toolkit;
using Roadnik.MAUI.ViewModels;
using static Roadnik.MAUI.Data.AppConsts;

namespace Roadnik.MAUI.Pages;

public partial class OptionsPage : CContentPage
{
  private readonly IPreferencesStorage p_storage;

  public OptionsPage()
  {
    InitializeComponent();
    p_storage = Container.Locate<IPreferencesStorage>();

    p_bleHrmDevice.TapCommand = new Command(OnBleHrmDeviceChanged);
  }

  private async void OnBleHrmDeviceChanged(object? _arg)
  {
    var bleWindowModel = new BleDevicesPageViewModel(
      _device =>
      {
        if (_device == null)
          p_storage.SetValue(PREF_BLE_HRM_DEVICE_INFO, null, PrefsStorageJsonCtx.Default.HrmDeviceInfo);
        else
          p_storage.SetValue(
            PREF_BLE_HRM_DEVICE_INFO, 
            new HrmDeviceInfo(_device.Id, _device.Name ?? string.Empty), 
            PrefsStorageJsonCtx.Default.HrmDeviceInfo);
      },
      () => Navigation.PopModalAsync());

    await Navigation.PushModalAsync(new BleDevicesPage(bleWindowModel));
  }

}
