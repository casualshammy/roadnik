using Ax.Fw.SharedTypes.Interfaces;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System.Reactive;

namespace Roadnik.MAUI.Interfaces;

internal interface IBleDevicesManager
{
  bool IsBluetoothAvailable { get; }

  Task<IReadOnlyList<IDevice>> ListDevicesAsync(
    ScanFilterOptions? _options,
    CancellationToken _ct);

  Task<bool> IsHrmDeviceAsync(
    IDevice _device,
    CancellationToken _ct);

  Task<ILifetime> SubscribeToHrmDataAsync(
    IDevice _device,
    bool _forceConnect,
    Action<int> _heartRateCallback,
    Action<Unit> _deviceDisconnectedCallback,
    CancellationToken _ct);

  Task<bool> TryConnectToDeviceByIdAsync(
    IDevice _device,
    CancellationToken _ct);

  Task TryDisconnectDeviceAsync(IDevice _device, CancellationToken _ct);
  Task ListDevicesAsync(Action<IDevice> _onDeviceFoundCallback, CancellationToken _ct);
}
