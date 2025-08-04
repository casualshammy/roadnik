using Ax.Fw.SharedTypes.Interfaces;
using Plugin.BLE.Abstractions.Contracts;
using Roadnik.MAUI.Interfaces;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace Roadnik.MAUI.ViewModels;

internal partial class BleDevicesPageViewModel : BaseViewModel
{
  private readonly IBleDevicesManager p_deviceSearcher;

  public BleDevicesPageViewModel()
  {
    ScanCommand = new Command(() =>
    {
      _ = ScanForDevicesAsync();
    });

    var lifetime = Container.Locate<IReadOnlyLifetime>();
    var pageLifetime = lifetime.GetChildLifetime();
    p_deviceSearcher = Container.Locate<IBleDevicesManager>();
  }

  public ObservableCollection<IDevice> Devices { get; } = [];
  public ICommand ScanCommand { get; }

  private async Task ScanForDevicesAsync()
  {
    var granted = await Permissions.RequestAsync<Permissions.Bluetooth>();
    if (granted != PermissionStatus.Granted)
    {
      await Permissions.RequestAsync<Permissions.Bluetooth>();
      return;
    }

    if (!p_deviceSearcher.IsBluetoothAvailable)
    {
      await Shell.Current.DisplayAlert("Bluetooth is not available or turned off", "Please enable Bluetooth to scan for devices.", "OK");
      return;
    }

    Devices.Clear();
    using var scanCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var devices = await p_deviceSearcher.ListDevicesAsync(scanCts.Token);
    foreach (var device in devices)
    {
      try
      {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var isHrmDevice = await p_deviceSearcher.IsHrmDeviceAsync(device, cts.Token);
        if (isHrmDevice)
          Devices.Add(device);

        var subscription = await p_deviceSearcher.SubscribeToHrmDataAsync(
          device,
          true,
          _hr => Debug.WriteLine($"Device: {device.Name}, HR: {_hr}"),
          cts.Token);
      }
      catch (Exception ex)
      {

      }
    }

  }
}
