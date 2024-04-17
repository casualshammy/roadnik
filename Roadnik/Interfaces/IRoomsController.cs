using Roadnik.Data;

namespace Roadnik.Interfaces;

public interface IRoomsController
{
  void RegisterRoom(
    string _roomId,
    string _email,
    int? _maxPoints,
    double? _minPointInterval,
    DateTimeOffset? _validUntil);

  void UnregisterRoom(string _roomId);
  RoomInfo? GetRoom(string _roomId);
  IReadOnlyList<RoomInfo> ListRegisteredRooms();
  void EnqueueUserWipe(string _roomId, string _username, long _upToDateTimeUnixMs);
}
