using L = Roadnik.MAUI.Resources.Strings.AppResources;

namespace Roadnik.MAUI.Pages;

public partial class LocationPermissionPage : ContentPage
{
  private readonly bool p_permissionAlways;
  private readonly Func<bool, Task> p_onClose;

  public LocationPermissionPage(
    bool _permissionAlways,
    Func<bool, Task> _onClose)
	{
    p_permissionAlways = _permissionAlways;
    p_onClose = _onClose;

    if (_permissionAlways)
      DescriptionText = L.page_location_permission_always;
    else
      DescriptionText = L.page_location_permission_app_in_use;

    OkButtonText = L.page_location_permission_ok_btn;
    CancelButtonText = L.page_location_permission_cancel_btn;

    InitializeComponent();
    BindingContext = this;
  }

  public string DescriptionText { get; }
  public string OkButtonText { get; }
  public string CancelButtonText { get; }

  private async void OnOkButtonClicked(object _s, EventArgs _e)
  {
    if (p_permissionAlways)
    {
      var osVersion = DeviceInfo.Current.Version;
      if (osVersion.Major < 11)
        await Permissions.RequestAsync<Permissions.LocationAlways>();
      else
        AppInfo.Current.ShowSettingsUI();
    }
    else
    {
      await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
    }

    await p_onClose(true);
    await Navigation.PopModalAsync(true);
  }

  private async void OnCancelButtonClicked(object _s, EventArgs _e)
  {
    await p_onClose(false);
    await Navigation.PopModalAsync(true);
  }

}