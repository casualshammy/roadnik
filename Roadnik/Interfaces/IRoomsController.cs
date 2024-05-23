using Roadnik.Data;

namespace Roadnik.Interfaces;

public interface IRoomsController
{
  void RegisterRoom(RoomInfo _roomInfo);

  void UnregisterRoom(string _roomId);
  RoomInfo? GetRoom(string _roomId);
  IReadOnlyList<RoomInfo> ListRegisteredRooms();
  void EnqueueUserWipe(string _roomId, string _username, long _upToDateTimeUnixMs);
  void EnqueuePathTruncate(string _roomId, string _username);
}
