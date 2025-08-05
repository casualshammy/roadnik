using Ax.Fw.SharedTypes.Interfaces;
using Plugin.BLE.Abstractions.Contracts;

namespace Roadnik.MAUI.Interfaces;

internal interface IBleDevicesManager
{
  bool IsBluetoothAvailable { get; }

  Task<IReadOnlyList<IDevice>> ListDevicesAsync(
    CancellationToken _ct);

  Task<bool> IsHrmDeviceAsync(
    IDevice _device,
    CancellationToken _ct);

  Task<IDisposable> SubscribeToHrmDataAsync(
    IDevice _device,
    bool _forceConnect,
    Action<int> _heartRateCallback,
    CancellationToken _ct);
  Task<IDevice?> TryConnectToDeviceByIdAsync(Guid _deviceGuid, CancellationToken _ct);
  Task TryDisconnectDeviceAsync(IDevice _device, CancellationToken _ct);
  Task ListDevicesAsync(Action<IDevice> _onDeviceFoundCallback, CancellationToken _ct);
}
