using Ax.Fw;
using Ax.Fw.SharedTypes.Interfaces;
using Plugin.BLE.Abstractions.Contracts;
using Roadnik.MAUI.Interfaces;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Roadnik.MAUI.ViewModels;

public partial class BleDevicesPageViewModel : BaseViewModel
{
  private readonly IBleDevicesManager p_deviceSearcher;
  private readonly ILifetime p_pageLifetime;

  public BleDevicesPageViewModel(
    Action<IDevice?> _onDeviceSelected,
    Action? _overrideCloseAction)
  {
    OnItemSelected = new Command(_o =>
    {
      if (_o is not IDevice device)
        return;

      _onDeviceSelected(device);

      if (_overrideCloseAction != null)
        _overrideCloseAction();
      else
        Shell.Current.GoToAsync("..");
    });

    p_deviceSearcher = Container.Locate<IBleDevicesManager>();

    var globalLifetime = Container.Locate<IReadOnlyLifetime>();
    p_pageLifetime = globalLifetime.GetChildLifetime() ?? new Lifetime();
  }

  public ObservableCollection<IDevice> Devices { get; } = [];
  public ICommand ScanCommand => new Command(async () => await ScanForDevicesAsync());
  public ICommand OnItemSelected { get; }
  public bool IsScanning { get; private set; }
  public int ScanningProgress { get; private set; }

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

    IsScanning = true;
    ScanningProgress = 0;
    OnPropertyChanged(nameof(IsScanning));
    OnPropertyChanged(nameof(ScanningProgress));

    try
    {
      Devices.Clear();

      var scanTimeout = TimeSpan.FromSeconds(10);
      using var scanTimedCts = new CancellationTokenSource(scanTimeout);
      using var scanCts = CancellationTokenSource.CreateLinkedTokenSource(p_pageLifetime.Token, scanTimedCts.Token);

      _ = Task.Run(async () =>
      {
        for (int i = 0; i < 50; i++)
        {
          if (scanCts.IsCancellationRequested)
            return;

          ScanningProgress += 1;
          OnPropertyChanged(nameof(ScanningProgress));
          await Task.Delay(scanTimeout / 50);
        }
      });

      await p_deviceSearcher.ListDevicesAsync(
        _d => Devices.Add(_d),
        scanCts.Token);

      var progressValue = (100 - ScanningProgress) / Math.Max(Devices.Count, 1);
      var devices = Devices.ToArray(); // To avoid modifying the collection while iterating
      foreach (var device in devices)
      {
        try
        {
          using var timedCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
          using var cts = CancellationTokenSource.CreateLinkedTokenSource(p_pageLifetime.Token, timedCts.Token);
          var isHrmDevice = await p_deviceSearcher.IsHrmDeviceAsync(device, cts.Token);
          if (!isHrmDevice)
            Devices.Remove(device);
        }
        catch
        {
          Devices.Remove(device);
        }

        ScanningProgress += progressValue;
        OnPropertyChanged(nameof(ScanningProgress));
      }
    }
    finally
    {
      IsScanning = false;
      OnPropertyChanged(nameof(IsScanning));
    }
  }
}
