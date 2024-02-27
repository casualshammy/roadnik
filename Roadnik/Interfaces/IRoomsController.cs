using Roadnik.Data;

namespace Roadnik.Interfaces;

public interface IRoomsController
{
  Task RegisterRoomAsync(
    string _roomId,
    string _email,
    int? _maxPoints,
    double? _minPointInterval,
    DateTimeOffset? _validUntil,
    CancellationToken _ct);

  Task UnregisterRoomAsync(string _roomId, CancellationToken _ct);
  Task<RoomInfo?> GetRoomAsync(string _roomId, CancellationToken _ct);
  Task<IReadOnlyList<RoomInfo>> ListRegisteredRoomsAsync(CancellationToken _ct);
  void EnqueueUserWipe(string _roomId, string _username, long _upToDateTimeUnixMs);
}
