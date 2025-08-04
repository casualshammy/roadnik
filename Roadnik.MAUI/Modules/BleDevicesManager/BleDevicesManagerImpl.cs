using Android.Gms.Common.Apis;
using Ax.Fw.DependencyInjection;
using Ax.Fw.Extensions;
using Ax.Fw.SharedTypes.Interfaces;
using Google.Android.Material.Color.Utilities;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Roadnik.MAUI.Interfaces;
using System.Collections.Concurrent;
using System.Reactive.Disposables;
using static Roadnik.MAUI.Data.Consts;

namespace Roadnik.MAUI.Modules.BleDevicesManager;

internal class BleDevicesManagerImpl : IBleDevicesManager, IAppModule<IBleDevicesManager>
{
  public static IBleDevicesManager ExportInstance(IAppDependencyCtx _ctx)
  {
    return _ctx.CreateInstance((
      IReadOnlyLifetime _lifetime,
      ILog _log) => new BleDevicesManagerImpl(_lifetime, _log["ble-devices-mgr"]));
  }

  private readonly ILog p_log;
  private readonly IBluetoothLE p_bluetoothLE;
  private readonly IAdapter p_adapter;
  private readonly SemaphoreSlim p_bleSemaphore = new(1, 1);
  private readonly ConcurrentDictionary<Guid, IObserver<int>> p_hrObservers = new();

  public BleDevicesManagerImpl(
    IReadOnlyLifetime _lifetime,
    ILog _log)
  {
    p_log = _log;
    p_bluetoothLE = CrossBluetoothLE.Current;
    p_adapter = p_bluetoothLE.Adapter;


  }

  public bool IsBluetoothAvailable => p_bluetoothLE.IsAvailable && p_bluetoothLE.IsOn;

  public async Task<IReadOnlyList<IDevice>> ListDevicesAsync(
    CancellationToken _ct)
  {
    await p_bleSemaphore.WaitAsync(_ct);
    try
    {
      var devices = new List<IDevice>();

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
        await p_adapter.StartScanningForDevicesAsync(cancellationToken: _ct);
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
    }
  }

  public async Task<IDevice?> TryConnectToDeviceByIdAsync(Guid _deviceGuid, CancellationToken _ct)
  {
    await p_bleSemaphore.WaitAsync(_ct);
    try
    {
      var device = await p_adapter.ConnectToKnownDeviceAsync(_deviceGuid, cancellationToken: _ct);
      return device;
    }
    catch
    {
      return null;
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
    try
    {
      await p_adapter.DisconnectDeviceAsync(_device);
    }
    catch
    { }
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
    try
    {
      await p_adapter.ConnectToDeviceAsync(_device, cancellationToken: _ct);

      try
      {
        var hrmService = await _device.GetServiceAsync(BLE_SERVICE_ID_HEART_RATE, _ct);
        if (hrmService == null)
          return false;

        var hrmCharacteristic = await hrmService.GetCharacteristicAsync(BLE_CHARACTERISTIC_ID_HEART_RATE_MEASUREMENT);
        if (hrmCharacteristic == null)
          return false;
        if (hrmCharacteristic.Properties != CharacteristicPropertyType.Notify)
          return false;

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

  public async Task<IDisposable> SubscribeToHrmDataAsync(
    IDevice _device,
    bool _forceConnect,
    Action<int> _heartRateCallback,
    CancellationToken _ct)
  {
    await p_bleSemaphore.WaitAsync(_ct);
    try
    {
      if (_forceConnect)
        await p_adapter.ConnectToDeviceAsync(_device, cancellationToken: _ct);

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

        await hrmCharacteristic.StartUpdatesAsync(_ct);

        return Disposable.Create(() =>
        {
          try
          {
            hrmCharacteristic.ValueUpdated -= onValueUpdated;
            _ = p_adapter.DisconnectDeviceAsync(_device);
          }
          catch (Exception ex)
          {
            p_log.Error($"Error while unsubscribing from HRM data of device '{_device.Name}' ({_device.Id}): {ex}");
          }
        });
      }
      catch
      {
        try
        {
          await p_adapter.DisconnectDeviceAsync(_device);
        }
        catch (Exception ex)
        {
          p_log.Error($"Error while catch-disconnecting from device '{_device.Name}' ({_device.Id}): {ex}");
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
