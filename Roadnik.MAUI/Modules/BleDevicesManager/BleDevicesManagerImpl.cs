using Android.Gms.Common.Apis;
using Ax.Fw;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Extensions;
using Roadnik.MAUI.Interfaces;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Modules.BleDevicesManager;

internal class BleDevicesManagerImpl : IBleDevicesManager, IAppModule<IBleDevicesManager>
{
  public static IBleDevicesManager ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      ILog _log) => new BleDevicesManagerImpl(_log["ble-devices-mgr"]));
  }

  private readonly ILog p_log;
  private readonly IBluetoothLE p_bluetoothLE;
  private readonly IAdapter p_adapter;
  private readonly SemaphoreSlim p_bleSemaphore = new(1, 1);
  private readonly Subject<IDevice> p_deviceConnectionBrokenSubj = new();

  public BleDevicesManagerImpl(
    ILog _log)
  {
    p_log = _log;
    p_bluetoothLE = CrossBluetoothLE.Current;
    p_adapter = p_bluetoothLE.Adapter;

    p_adapter.DeviceConnectionError += (_s, _e) =>
    {
      var device = _e.Device;
      if (device == null)
      {
        p_log.Error($"Unknown device connection error: {_e.ErrorMessage}");
        return;
      }

      p_log.Error($"Connection error to device {_e.Device?.Name} ({_e.Device?.Id}) is occured: {_e.ErrorMessage}");
      p_deviceConnectionBrokenSubj.OnNext(device);
    };

    p_adapter.DeviceConnectionLost += (_s, _e) =>
    {
      var device = _e.Device;
      if (device == null)
      {
        p_log.Error($"Unknown device connection lost: {_e.ErrorMessage}");
        return;
      }

      p_log.Error($"Connection to device '{device.Name}' ({device.Id}) is lost: {_e.ErrorMessage}");
      p_deviceConnectionBrokenSubj.OnNext(device);
    };

    p_adapter.DeviceDisconnected += (_s, _e) =>
    {
      var device = _e.Device;
      if (device == null)
      {
        p_log.Error($"Unknown device disconnected");
        return;
      }

      p_log.Info($"Device '{device.Name}' ({device.Id}) is disconnected");
      p_deviceConnectionBrokenSubj.OnNext(device);
    };
  }

  public bool IsBluetoothAvailable => p_bluetoothLE.IsAvailable && p_bluetoothLE.IsOn;

  public async Task<IReadOnlyList<IDevice>> ListDevicesAsync(
    ScanFilterOptions? _options,
    CancellationToken _ct)
  {
    await p_bleSemaphore.WaitAsync(_ct);
    p_log.Info("Starting BLE device scan...");

    var devices = new List<IDevice>();
    try
    {
      void onDeviceDiscovered(
        object? _s,
        DeviceEventArgs _ev)
      {
        var device = _ev.Device;
        if (device == null || device.Name.IsNullOrWhiteSpace())
          return;

        devices.Add(device);
      }

      p_adapter.DeviceDiscovered += onDeviceDiscovered;
      try
      {
        await p_adapter.StopScanningForDevicesAsync();
        await p_adapter.StartScanningForDevicesAsync(scanFilterOptions: _options, cancellationToken: _ct);
        return devices;
      }
      catch (Exception ex)
      {
        p_log.Error($"Error while scanning for BLE devices: {ex}");
        return devices;
      }
      finally
      {
        p_adapter.DeviceDiscovered -= onDeviceDiscovered;
      }
    }
    finally
    {
      p_bleSemaphore.Release();
      p_log.Info($"BLE device scan completed; total devices: '{devices.Count}'");
    }
  }

  public async Task ListDevicesAsync(
    Action<IDevice> _onDeviceFoundCallback,
    CancellationToken _ct)
  {
    await p_bleSemaphore.WaitAsync(_ct);
    p_log.Info("Starting BLE device scan...");

    var deviceCounter = 0;
    try
    {
      void onDeviceDiscovered(
        object? _s,
        DeviceEventArgs _ev)
      {
        var device = _ev.Device;
        if (device != null)
        {
          _onDeviceFoundCallback(device);
          Interlocked.Increment(ref deviceCounter);
        }
      }

      p_adapter.DeviceDiscovered += onDeviceDiscovered;
      try
      {
        await p_adapter.StopScanningForDevicesAsync();
        await p_adapter.StartScanningForDevicesAsync(cancellationToken: _ct);
      }
      catch (Exception ex)
      {
        p_log.Error($"Error while scanning for BLE devices: {ex}");
      }
      finally
      {
        p_adapter.DeviceDiscovered -= onDeviceDiscovered;
      }
    }
    finally
    {
      p_bleSemaphore.Release();
      p_log.Info($"BLE device scan completed; total devices: '{deviceCounter}'");
    }
  }

  public async Task<bool> TryConnectToDeviceByIdAsync(
    IDevice _device,
    CancellationToken _ct)
  {
    await p_bleSemaphore.WaitAsync(_ct);
    p_log.Info($"Trying to connect to BLE device with ID '{_device.Id}'...");

    try
    {
      await p_adapter.ConnectToDeviceAsync(_device, cancellationToken: _ct);

      p_log.Info($"Connected to BLE device '{_device.Name}' ({_device.Id})");
      return true;
    }
    catch (Exception ex)
    {
      p_log.Error($"Failed to connect to BLE device with ID '{_device.Id}': {ex}");
      return false;
    }
    finally
    {
      p_bleSemaphore.Release();
    }
  }

  public async Task TryDisconnectDeviceAsync(
    IDevice _device,
    CancellationToken _ct)
  {
    await p_bleSemaphore.WaitAsync(_ct);
    p_log.Info($"Trying to disconnect from BLE device '{_device.Name}' ({_device.Id})...");

    try
    {
      await p_adapter.DisconnectDeviceAsync(_device);
      p_log.Info($"Disconnected from BLE device '{_device.Name}' ({_device.Id})");
    }
    catch (Exception ex)
    {
      p_log.Error($"Failed to disconnect from BLE device '{_device.Name}' ({_device.Id}): {ex}");
    }
    finally
    {
      p_bleSemaphore.Release();
    }
  }

  public async Task<bool> IsHrmDeviceAsync(
    IDevice _device,
    CancellationToken _ct)
  {
    await p_bleSemaphore.WaitAsync(_ct);
    p_log.Info($"Checking if device '{_device.Name}' ({_device.Id}) is a Heart Rate Monitor (HRM)...");

    try
    {
      await p_adapter.ConnectToDeviceAsync(_device, cancellationToken: _ct);

      try
      {
        var hrmService = await _device.GetServiceAsync(BLE_SERVICE_ID_HEART_RATE, _ct);
        if (hrmService == null)
        {
          p_log.Info($"Device '{_device.Name}' ({_device.Id}) does not have the Heart Rate service.");
          return false;
        }

        var hrmCharacteristic = await hrmService.GetCharacteristicAsync(BLE_CHARACTERISTIC_ID_HEART_RATE_MEASUREMENT);
        if (hrmCharacteristic == null)
        {
          p_log.Info($"Device '{_device.Name}' ({_device.Id}) does not have the Heart Rate Measurement characteristic.");
          return false;
        }

        if (hrmCharacteristic.Properties != CharacteristicPropertyType.Notify)
        {
          p_log.Info($"Characteristic '{BLE_CHARACTERISTIC_ID_HEART_RATE_MEASUREMENT}' on device '{_device.Name}' ({_device.Id}) does not support notifications.");
          return false;
        }

        p_log.Info($"Device '{_device.Name}' ({_device.Id}) is a Heart Rate Monitor (HRM).");
        return true;
      }
      finally
      {
        await p_adapter.DisconnectDeviceAsync(_device);
      }
    }
    finally
    {
      p_bleSemaphore.Release();
    }
  }

  public async Task<ILifetime> SubscribeToHrmDataAsync(
    IDevice _device,
    bool _forceConnect,
    Action<int> _heartRateCallback,
    Action<Unit> _deviceDisconnectedCallback,
    CancellationToken _ct)
  {
    await p_bleSemaphore.WaitAsync(_ct);
    p_log.Info($"Subscribing to Heart Rate Monitor (HRM) data for device '{_device.Name}' ({_device.Id})...");

    var lifetime = new Lifetime();

    try
    {
      if (_forceConnect)
        await p_adapter.ConnectToDeviceAsync(_device, cancellationToken: _ct);

      lifetime.DoOnEnding(async () =>
      {
        try
        {
          await p_adapter.DisconnectDeviceAsync(_device);
        }
        catch (Exception ex)
        {
          p_log.Error($"Error while disconnecting from subscribed device '{_device.Name}' ({_device.Id}): {ex}");
        }
      });

      try
      {
        var hrmService = await _device.GetServiceAsync(BLE_SERVICE_ID_HEART_RATE, cancellationToken: _ct)
          ?? throw new InvalidOperationException($"Service '{BLE_SERVICE_ID_HEART_RATE}' not found on device '{_device.Name}' ({_device.Id})");

        var hrmCharacteristic = await hrmService.GetCharacteristicAsync(BLE_CHARACTERISTIC_ID_HEART_RATE_MEASUREMENT)
          ?? throw new InvalidOperationException($"Characteristic '{BLE_CHARACTERISTIC_ID_HEART_RATE_MEASUREMENT}' not found on device '{_device.Name}' ({_device.Id})");
        if (hrmCharacteristic.Properties != CharacteristicPropertyType.Notify)
          throw new InvalidOperationException($"Characteristic '{BLE_CHARACTERISTIC_ID_HEART_RATE_MEASUREMENT}' on device '{_device.Name}' ({_device.Id}) does not support notifications");

        void onValueUpdated(
          object? _s,
          CharacteristicUpdatedEventArgs _hrmEvData)
        {
          var bytes = _hrmEvData.Characteristic.Value;
          if (bytes == null || bytes.Length < 2)
            return;

          var hr = bytes[1];
          _heartRateCallback(hr);
        }

        hrmCharacteristic.ValueUpdated += onValueUpdated;
        lifetime.DoOnEnding(() =>
        {
          hrmCharacteristic.ValueUpdated -= onValueUpdated;
        });

        await hrmCharacteristic.StartUpdatesAsync(_ct);
        lifetime.DoOnEnding(async () =>
        {
          try
          {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await hrmCharacteristic.StopUpdatesAsync(cts.Token);
          }
          catch (Exception ex)
          {
            p_log.Error($"Error while stopping HR updates from subscribed device '{_device.Name}' ({_device.Id}): {ex}");
          }
        });

        p_deviceConnectionBrokenSubj
          .Where(_ => _.Id == _device.Id)
          .Subscribe(_ =>
          {
            p_log.Info($"Device '{_device.Name}' ({_device.Id}) connection is broken, stopping HRM updates");
            _deviceDisconnectedCallback(Unit.Default);
          }, lifetime);

        return lifetime;
      }
      catch (Exception ex)
      {
        p_log.Error($"Error while subscribing to HRM data of device '{_device.Name}' ({_device.Id}): {ex}");
        try
        {
          await p_adapter.DisconnectDeviceAsync(_device);
        }
        catch (Exception disconnectEx)
        {
          p_log.Error($"Error while catch-disconnecting from device '{_device.Name}' ({_device.Id}): {disconnectEx}");
        }
        throw;
      }
    }
    finally
    {
      p_bleSemaphore.Release();
    }
  }

}
